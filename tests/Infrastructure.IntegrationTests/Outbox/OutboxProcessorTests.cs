using System.Collections.Concurrent;
using CqrsApiExample.Application.Common.Events;
using CqrsApiExample.Domain.UserAggregate;
using CqrsApiExample.Domain.UserAggregate.Events;
using CqrsApiExample.Infrastructure.Outbox;
using CqrsApiExample.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using TheTesting.Shared;

namespace CqrsApiExample.Infrastructure.IntegrationTests.Outbox;

/// <summary>
///     The outbox against a real SQL Server: messages are written by the unit of
///     work exactly as in production, then handled by the processor while the
///     test controls the clock and the handlers' behaviour.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class OutboxProcessorTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private readonly TestDatabase _database = new(sqlServer);
    private readonly ReliableHandler _reliable = new();
    private readonly FlakyHandler _flaky = new();
    private readonly FakeLogger<OutboxProcessor> _logger = new();
    private readonly OutboxOptions _options = new();

    public Task InitializeAsync() => _database.CreateAsync();

    public Task DisposeAsync() => _database.DeleteAsync();

    [Fact]
    [Trait("Category", "Outbox")]
    public async Task Pending_Message_Is_Handled_Once_And_Marked_As_Processed()
    {
        // Arrange: a committed change whose event was never handled (e.g. the process stopped)
        var user = await AddUserAsync("john@doe.com");

        // Act
        var claimed = await ProcessBatchAsync(_reliable);
        var claimedAgain = await ProcessBatchAsync(_reliable);

        // Assert
        claimed.Should().Be(1);
        claimedAgain.Should().Be(0);
        _reliable.Handled.Should().ContainSingle().Which.Should().Be(
            new UserCreatedDomainEvent(user.Id, "John", "Doe", "john@doe.com"));

        var message = await SingleMessageAsync();
        message.ProcessedAt.Should().Be(_database.Clock.Now.UtcDateTime);
        message.LockedUntil.Should().BeNull();
        await using var context = _database.CreateContext();
        (await context.OutboxMessageConsumers.SingleAsync()).HandlerName.Should().Be(typeof(ReliableHandler).FullName);
    }

    [Fact]
    [Trait("Category", "Outbox")]
    public async Task Failed_Message_Is_Retried_With_Backoff_Until_It_Succeeds()
    {
        // Arrange
        await AddUserAsync("john@doe.com");
        _flaky.FailuresLeft = 2;

        // Act & Assert: first failure, retried after the base delay
        await ProcessBatchAsync(_flaky);
        var message = await SingleMessageAsync();
        message.Attempts.Should().Be(1);
        message.Error.Should().Contain("SMTP is down");
        message.NextAttemptAt.Should().Be((_database.Clock.Now + _options.BaseRetryDelay).UtcDateTime);
        message.ProcessedAt.Should().BeNull();

        (await ProcessBatchAsync(_flaky)).Should().Be(0, "the message is not due yet");

        // second failure: the delay doubles
        _database.Clock.Advance(_options.BaseRetryDelay);
        await ProcessBatchAsync(_flaky);
        (await SingleMessageAsync()).NextAttemptAt
            .Should().Be((_database.Clock.Now + 2 * _options.BaseRetryDelay).UtcDateTime);

        // third attempt succeeds
        _database.Clock.Advance(2 * _options.BaseRetryDelay);
        await ProcessBatchAsync(_flaky);
        message = await SingleMessageAsync();
        message.ProcessedAt.Should().NotBeNull();
        message.Error.Should().BeNull();
        message.Attempts.Should().Be(2);
        _flaky.Calls.Should().Be(3);
        _logger.Collector.GetSnapshot().Should().OnlyContain(record => record.Level == LogLevel.Warning);
    }

    [Fact]
    [Trait("Category", "Outbox")]
    public async Task Retry_Does_Not_Run_Handlers_That_Already_Succeeded()
    {
        // Arrange
        await AddUserAsync("john@doe.com");
        _flaky.FailuresLeft = 1;

        // Act
        await ProcessBatchAsync(_reliable, _flaky);
        _database.Clock.Advance(_options.BaseRetryDelay);
        await ProcessBatchAsync(_reliable, _flaky);

        // Assert: the reliable handler (e.g. an email) is not repeated because the other one failed
        _reliable.Handled.Should().HaveCount(1);
        _flaky.Calls.Should().Be(2);
        (await SingleMessageAsync()).ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Outbox")]
    public async Task Message_Is_Given_Up_After_Max_Attempts_And_Logged_As_Error()
    {
        // Arrange
        await AddUserAsync("john@doe.com");
        _options.MaxAttempts = 2;
        _flaky.FailuresLeft = int.MaxValue;

        // Act
        await ProcessBatchAsync(_flaky);
        _database.Clock.Advance(_options.MaxRetryDelay);
        await ProcessBatchAsync(_flaky);
        _database.Clock.Advance(TimeSpan.FromDays(1));
        var claimedAfterGivingUp = await ProcessBatchAsync(_flaky);

        // Assert: kept with its error for diagnosis (dead letter), never claimed again
        claimedAfterGivingUp.Should().Be(0);
        var message = await SingleMessageAsync();
        message.Attempts.Should().Be(2);
        message.ProcessedAt.Should().BeNull();
        message.Error.Should().Contain("SMTP is down");
        _logger.Collector.LatestRecord.Level.Should().Be(LogLevel.Error);
        _logger.Collector.LatestRecord.Message.Should().Contain("will not be retried");
    }

    [Fact]
    [Trait("Category", "Outbox")]
    public async Task Message_Leased_By_A_Processor_That_Died_Is_Picked_Up_When_The_Lease_Expires()
    {
        // Arrange: a processor claimed the message and stopped before finishing
        await AddUserAsync("john@doe.com");
        await using (var context = _database.CreateContext())
        {
            var lockedUntil = (_database.Clock.Now + _options.LockDuration).UtcDateTime;
            await context.OutboxMessages.ExecuteUpdateAsync(setters =>
                setters.SetProperty(message => message.LockedUntil, lockedUntil));
        }

        // Act
        var whileLeased = await ProcessBatchAsync(_reliable);
        _database.Clock.Advance(_options.LockDuration + TimeSpan.FromSeconds(1));
        var afterLease = await ProcessBatchAsync(_reliable);

        // Assert
        whileLeased.Should().Be(0);
        afterLease.Should().Be(1);
        _reliable.Handled.Should().HaveCount(1);
    }

    [Fact]
    [Trait("Category", "Outbox")]
    public async Task Concurrent_Processors_Never_Handle_The_Same_Message()
    {
        // Arrange: like two API instances working on the same outbox
        const int users = 12;
        for (var i = 0; i < users; i++)
        {
            await AddUserAsync($"user{i}@doe.com");
        }
        _options.BatchSize = 3;

        // Act
        await Task.WhenAll(DrainAsync(), DrainAsync());

        // Assert
        _reliable.Handled.Should().HaveCount(users);
        _reliable.Handled.Select(handled => handled.UserId).Should().OnlyHaveUniqueItems();

        async Task DrainAsync()
        {
            while (await ProcessBatchAsync(_reliable) > 0)
            {
            }
        }
    }

    private async Task<User> AddUserAsync(string email)
    {
        var user = User.Create("John", "Doe", email).Value;
        await using var context = _database.CreateContext();
        new UserRepository(context).Add(user);
        await _database.CreateUnitOfWork(context).SaveChangesAsync(default);
        return user;
    }

    private async Task<int> ProcessBatchAsync(params IDomainEventHandler<UserCreatedDomainEvent>[] handlers)
    {
        var services = new ServiceCollection();
        foreach (var handler in handlers)
        {
            services.AddSingleton(handler);
        }
        await using var provider = services.BuildServiceProvider();
        await using var context = _database.CreateContext();

        var processor = new OutboxProcessor(context,
                                            new DomainEventDispatcher(provider),
                                            _database.Clock,
                                            Options.Create(_options),
                                            _logger);
        return await processor.ProcessBatchAsync(default);
    }

    private async Task<OutboxMessage> SingleMessageAsync()
    {
        await using var context = _database.CreateContext();
        return await context.OutboxMessages.SingleAsync();
    }

    private sealed class ReliableHandler : IDomainEventHandler<UserCreatedDomainEvent>
    {
        public ConcurrentQueue<UserCreatedDomainEvent> Handled { get; } = new();

        public Task Handle(UserCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            Handled.Enqueue(domainEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FlakyHandler : IDomainEventHandler<UserCreatedDomainEvent>
    {
        public int FailuresLeft { get; set; }

        public int Calls { get; private set; }

        public Task Handle(UserCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            Calls++;
            if (FailuresLeft-- > 0)
            {
                throw new InvalidOperationException("SMTP is down");
            }

            return Task.CompletedTask;
        }
    }
}
