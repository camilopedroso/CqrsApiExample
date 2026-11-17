using CqrsApiExample.Application.Common.Exceptions;
using CqrsApiExample.Application.Users;
using CqrsApiExample.Domain.UserAggregate;
using CqrsApiExample.Domain.UserAggregate.Events;
using CqrsApiExample.Infrastructure.Outbox;
using CqrsApiExample.Infrastructure.Persistence.Queries;
using CqrsApiExample.Infrastructure.Persistence.Repositories;
using CqrsApiExample.Tests.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CqrsApiExample.Infrastructure.IntegrationTests.Persistence;

/// <summary>
///     Runs against a real SQL Server (Testcontainers) with the migrations
///     applied, so mapping, unique index and interceptors are all exercised.
///     Every test gets its own database.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class PersistenceTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private readonly TestDatabase _database = new(sqlServer);

    public Task InitializeAsync() => _database.CreateAsync();

    public Task DisposeAsync() => _database.DeleteAsync();

    [Fact]
    [Trait("Category", "Persistence")]
    public async Task SaveChanges_Should_Persist_User_And_Fill_CreatedAt()
    {
        // Arrange
        var user = User.Create("John", "Doe", "john@doe.com").Value;
        await using (var context = _database.CreateContext())
        {
            new UserRepository(context).Add(user);

            // Act
            await _database.CreateUnitOfWork(context).SaveChangesAsync(default);
        }

        // Assert
        await using var readContext = _database.CreateContext();
        var saved = await readContext.Users.SingleAsync(u => u.Id == user.Id);
        saved.Should().Be(user);
        saved.Email.Should().Be("john@doe.com");
        saved.CreatedAt.Should().Be(TestDatabase.Start.UtcDateTime);
        saved.ModifiedAt.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Persistence")]
    public async Task SaveChanges_Should_Throw_UniqueConstraintViolation_When_Email_Is_Duplicated()
    {
        // Arrange
        await AddUserAsync(User.Create("John", "Doe", "john@doe.com").Value);

        await using var otherContext = _database.CreateContext();
        new UserRepository(otherContext).Add(User.Create("Johnny", "Doe", "john@doe.com").Value);

        // Act
        var act = () => _database.CreateUnitOfWork(otherContext).SaveChangesAsync(default);

        // Assert
        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    [Trait("Category", "Persistence")]
    public async Task ExistsByEmailAsync_Should_Find_Only_Saved_Emails()
    {
        // Arrange
        await AddUserAsync(User.Create("John", "Doe", "john@doe.com").Value);
        await using var context = _database.CreateContext();
        var repository = new UserRepository(context);

        // Act & Assert
        (await repository.ExistsByEmailAsync("john@doe.com", default)).Should().BeTrue();
        (await repository.ExistsByEmailAsync("jane@doe.com", default)).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Persistence")]
    public async Task UserQueries_Should_Return_Read_Models_Ordered_By_Creation_Without_Tracking()
    {
        // Arrange
        var first = User.Create("Zoe", "Doe", "zoe@doe.com").Value;
        var second = User.Create("Adam", "Doe", "adam@doe.com").Value;
        await AddUserAsync(first);
        _database.Clock.Advance(TimeSpan.FromMinutes(1));
        await AddUserAsync(second);

        await using var readContext = _database.CreateContext();
        var queries = new UserQueries(readContext);

        // Act
        var all = await queries.GetAllAsync(default);
        var byId = await queries.GetByIdAsync(second.Id, default);
        var missing = await queries.GetByIdAsync(Guid.NewGuid(), default);

        // Assert
        all.Should().Equal(UserDto.From(first), UserDto.From(second));
        byId.Should().Be(UserDto.From(second));
        missing.Should().BeNull();
        readContext.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Outbox")]
    public async Task SaveChanges_Should_Store_Domain_Events_In_The_Outbox_With_The_Changes()
    {
        // Arrange
        var user = User.Create("John", "Doe", "john@doe.com").Value;

        // Act
        await AddUserAsync(user);

        // Assert
        await using var readContext = _database.CreateContext();
        var message = await readContext.OutboxMessages.SingleAsync();
        message.Type.Should().Be(typeof(UserCreatedDomainEvent).FullName);
        OutboxSerializer.Deserialize(message.Type, message.Content)
                        .Should().Be(new UserCreatedDomainEvent(user.Id, "John", "Doe", "john@doe.com"));
        message.OccurredAt.Should().Be(TestDatabase.Start.UtcDateTime);
        message.NextAttemptAt.Should().Be(TestDatabase.Start.UtcDateTime);
        message.ProcessedAt.Should().BeNull();
        message.Attempts.Should().Be(0);
        user.GetDomainEvents().Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Outbox")]
    public async Task SaveChanges_Should_Not_Store_Domain_Events_When_The_Save_Fails()
    {
        // Arrange
        await AddUserAsync(User.Create("John", "Doe", "john@doe.com").Value);

        await using var otherContext = _database.CreateContext();
        new UserRepository(otherContext).Add(User.Create("Johnny", "Doe", "john@doe.com").Value);

        // Act
        var act = () => _database.CreateUnitOfWork(otherContext).SaveChangesAsync(default);

        // Assert: the rejected user's event was rolled back with it
        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
        await using var readContext = _database.CreateContext();
        (await readContext.OutboxMessages.CountAsync()).Should().Be(1);
    }

    private async Task AddUserAsync(User user)
    {
        await using var context = _database.CreateContext();
        new UserRepository(context).Add(user);
        await _database.CreateUnitOfWork(context).SaveChangesAsync(default);
    }
}
