using CqrsApiExample.Application.Auth;
using CqrsApiExample.Application.Common.Exceptions;
using CqrsApiExample.Application.Users;
using CqrsApiExample.Domain.UserAggregate;
using CqrsApiExample.Domain.UserAggregate.Events;
using CqrsApiExample.Infrastructure.Outbox;
using CqrsApiExample.Infrastructure.Persistence.Ado;
using CqrsApiExample.Infrastructure.Persistence.Queries;
using CqrsApiExample.Tests.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CqrsApiExample.Infrastructure.IntegrationTests.Persistence;

/// <summary>The ADO.NET provider sending each unit of work as one batched command (the default).</summary>
[Collection(SqlServerCollection.Name)]
public sealed class AdoPersistenceTests(SqlServerFixture sqlServer)
    : AdoPersistenceContract(sqlServer, AdoWriteMode.Batched);

/// <summary>The ADO.NET provider sending one command per statement in a client-side transaction.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class AdoCommandPerStatementPersistenceTests(SqlServerFixture sqlServer)
    : AdoPersistenceContract(sqlServer, AdoWriteMode.CommandPerStatement);

/// <summary>
///     The ADO.NET provider against a real SQL Server, with the same scenarios as
///     <see cref="PersistenceTests"/>. Rows are read back through EF Core (and the
///     other way round), so both providers are proven to agree on the schema.
///     Runs once per <see cref="AdoWriteMode"/>.
/// </summary>
public abstract class AdoPersistenceContract : IAsyncLifetime
{
    private readonly TestDatabase _database;
    private readonly AdoWriteMode _mode;

    // private protected: AdoWriteMode is internal to the infrastructure.
    private protected AdoPersistenceContract(SqlServerFixture sqlServer, AdoWriteMode mode)
    {
        _database = new TestDatabase(sqlServer);
        _mode = mode;
    }

    public Task InitializeAsync() => _database.CreateAsync();

    public Task DisposeAsync() => _database.DeleteAsync();

    [Fact]
    [Trait("Category", "Persistence")]
    public async Task SaveChanges_Should_Persist_User_And_Fill_CreatedAt()
    {
        // Arrange
        var user = User.Create("John", "Doe", "john@doe.com", "hashed-password").Value;
        var ado = _database.CreateAdoScope(_mode);
        ado.Repository.Add(user);

        // Act
        await ado.UnitOfWork.SaveChangesAsync(default);

        // Assert
        user.CreatedAt.Should().Be(TestDatabase.Start.UtcDateTime);
        await using var readContext = _database.CreateContext();
        var saved = await readContext.Users.SingleAsync(u => u.Id == user.Id);
        saved.Name.Should().Be("John");
        saved.Surname.Should().Be("Doe");
        saved.Email.Should().Be("john@doe.com");
        saved.CreatedAt.Should().Be(TestDatabase.Start.UtcDateTime);
        saved.ModifiedAt.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Persistence")]
    public async Task SaveChanges_Should_Do_Nothing_When_Nothing_Was_Added()
    {
        // Act
        await _database.CreateAdoScope(_mode).UnitOfWork.SaveChangesAsync(default);

        // Assert
        await using var readContext = _database.CreateContext();
        (await readContext.Users.CountAsync()).Should().Be(0);
        (await readContext.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Persistence")]
    public async Task SaveChanges_Should_Persist_Several_Users_And_Their_Events_Together()
    {
        // Arrange: two aggregates in one unit of work, so parameter names must not collide
        var john = User.Create("John", "Doe", "john@doe.com", "hashed-password").Value;
        var jane = User.Create("Jane", "Doe", "jane@doe.com", "hashed-password").Value;
        var ado = _database.CreateAdoScope(_mode);
        ado.Repository.Add(john);
        ado.Repository.Add(jane);

        // Act
        await ado.UnitOfWork.SaveChangesAsync(default);

        // Assert
        await using var readContext = _database.CreateContext();
        (await readContext.Users.Select(u => u.Email).ToListAsync())
            .Should().BeEquivalentTo("john@doe.com", "jane@doe.com");
        (await readContext.OutboxMessages.Select(m => m.Content).ToListAsync())
            .Select(content => OutboxSerializer.Deserialize(typeof(UserCreatedDomainEvent).FullName!, content))
            .Should().BeEquivalentTo([
                new UserCreatedDomainEvent(john.Id, "John", "Doe", "john@doe.com"),
                new UserCreatedDomainEvent(jane.Id, "Jane", "Doe", "jane@doe.com")
            ]);
    }

    [Fact]
    [Trait("Category", "Persistence")]
    public async Task SaveChanges_Should_Throw_UniqueConstraintViolation_When_Email_Is_Duplicated()
    {
        // Arrange
        await AddUserAsync(User.Create("John", "Doe", "john@doe.com", "hashed-password").Value);
        var ado = _database.CreateAdoScope(_mode);
        ado.Repository.Add(User.Create("Johnny", "Doe", "john@doe.com", "hashed-password").Value);

        // Act
        var act = () => ado.UnitOfWork.SaveChangesAsync(default);

        // Assert
        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    [Trait("Category", "Persistence")]
    public async Task ExistsByEmailAsync_Should_Find_Only_Saved_Emails()
    {
        // Arrange
        await AddUserAsync(User.Create("John", "Doe", "john@doe.com", "hashed-password").Value);
        var repository = _database.CreateAdoScope(_mode).Repository;

        // Act & Assert
        (await repository.ExistsByEmailAsync("john@doe.com", default)).Should().BeTrue();
        (await repository.ExistsByEmailAsync("jane@doe.com", default)).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Persistence")]
    public async Task UserQueries_Should_Return_Read_Models_Ordered_By_Creation()
    {
        // Arrange
        var first = User.Create("Zoe", "Doe", "zoe@doe.com", "hashed-password").Value;
        var second = User.Create("Adam", "Doe", "adam@doe.com", "hashed-password").Value;
        await AddUserAsync(first);
        _database.Clock.Advance(TimeSpan.FromMinutes(1));
        await AddUserAsync(second);
        var queries = _database.CreateAdoScope(_mode).Queries;

        // Act
        var all = await queries.GetAllAsync(default);
        var byId = await queries.GetByIdAsync(second.Id, default);
        var missing = await queries.GetByIdAsync(Guid.NewGuid(), default);

        // Assert
        all.Should().Equal(UserDto.From(first), UserDto.From(second));
        byId.Should().Be(UserDto.From(second));
        missing.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Persistence")]
    public async Task UserQueries_Should_Return_The_Same_As_The_EF_Core_Queries()
    {
        // Arrange
        await AddUserAsync(User.Create("Zoe", "Doe", "zoe@doe.com", "hashed-password").Value);
        _database.Clock.Advance(TimeSpan.FromMinutes(1));
        await AddUserAsync(User.Create("Adam", "Doe", "adam@doe.com", "hashed-password").Value);
        await using var context = _database.CreateContext();

        // Act
        var fromAdo = await _database.CreateAdoScope(_mode).Queries.GetAllAsync(default);
        var fromEfCore = await new UserQueries(context).GetAllAsync(default);

        // Assert
        fromAdo.Should().Equal(fromEfCore);
    }

    [Fact]
    [Trait("Category", "Persistence")]
    public async Task GetCredentialsByEmailAsync_Should_Return_The_Stored_Hash_And_Role_Like_EF_Core()
    {
        // Arrange: inserted by the ADO.NET unit of work, so the hash column is written by hand-written SQL too
        var user = User.Create("John", "Doe", "john@doe.com", "stored-hash").Value;
        user.GrantAdmin();
        await AddUserAsync(user);
        await using var context = _database.CreateContext();

        // Act
        var fromAdo = await _database.CreateAdoScope(_mode).Queries.GetCredentialsByEmailAsync("john@doe.com", default);
        var fromEfCore = await new UserQueries(context).GetCredentialsByEmailAsync("john@doe.com", default);
        var missing = await _database.CreateAdoScope(_mode).Queries.GetCredentialsByEmailAsync("jane@doe.com", default);

        // Assert
        fromAdo.Should().Be(new UserCredentialsDto(user.Id, "stored-hash", UserRole.Admin));
        fromEfCore.Should().Be(fromAdo);
        missing.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Outbox")]
    public async Task SaveChanges_Should_Store_Domain_Events_In_The_Outbox_With_The_Changes()
    {
        // Arrange
        var user = User.Create("John", "Doe", "john@doe.com", "hashed-password").Value;

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
        message.LockedUntil.Should().BeNull();
        message.Attempts.Should().Be(0);
        user.GetDomainEvents().Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Outbox")]
    public async Task SaveChanges_Should_Not_Store_Domain_Events_When_The_Save_Fails()
    {
        // Arrange
        await AddUserAsync(User.Create("John", "Doe", "john@doe.com", "hashed-password").Value);
        var rejected = User.Create("Johnny", "Doe", "john@doe.com", "hashed-password").Value;
        var ado = _database.CreateAdoScope(_mode);
        ado.Repository.Add(rejected);

        // Act
        var act = () => ado.UnitOfWork.SaveChangesAsync(default);

        // Assert: the transaction was rolled back, event included (in batched mode that is
        // XACT_ABORT's job); the aggregate keeps its event
        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
        await using var readContext = _database.CreateContext();
        (await readContext.OutboxMessages.CountAsync()).Should().Be(1);
        (await readContext.Users.CountAsync()).Should().Be(1);
        rejected.GetDomainEvents().Should().ContainSingle();
    }

    private async Task AddUserAsync(User user)
    {
        var ado = _database.CreateAdoScope(_mode);
        ado.Repository.Add(user);
        await ado.UnitOfWork.SaveChangesAsync(default);
    }
}
