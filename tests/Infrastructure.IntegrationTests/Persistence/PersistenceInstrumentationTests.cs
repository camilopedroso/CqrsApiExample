using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Application.Users;
using CqrsApiExample.Infrastructure.Persistence;
using CqrsApiExample.Infrastructure.Persistence.Decorators;
using CqrsApiExample.Infrastructure.Persistence.Instrumentation;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Moq;

namespace CqrsApiExample.Infrastructure.IntegrationTests.Persistence;

/// <summary>No database: the decorators time a stubbed provider.</summary>
public sealed class PersistenceInstrumentationTests : IDisposable
{
    private readonly FakeLogger<PersistenceInstrumentation> _logger = new();
    private readonly MetricCollector<double> _metrics = new(meterScope: null,
                                                            PersistenceInstrumentation.MeterName,
                                                            PersistenceInstrumentation.DurationInstrumentName);
    private readonly Mock<IUserQueries> _inner = new();

    public void Dispose() => _metrics.Dispose();

    [Fact]
    [Trait("Category", "Persistence")]
    public async Task Successful_Call_Should_Be_Logged_And_Recorded_With_Provider_And_Operation()
    {
        // Arrange
        var users = new List<UserDto> { new(Guid.NewGuid(), "John", "Doe", "john@doe.com") };
        _inner.Setup(queries => queries.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(users);
        var queries = CreateQueries(PersistenceProvider.Ado);

        // Act
        var result = await queries.GetAllAsync(default);

        // Assert
        result.Should().BeSameAs(users);

        var log = _logger.Collector.GetSnapshot().Should().ContainSingle().Subject;
        log.Level.Should().Be(LogLevel.Information);
        log.Message.Should().StartWith("Persistence Ado UserQueries.GetAll succeeded in ").And.EndWith(" ms");

        var measurement = OwnMeasurements().Should().ContainSingle().Subject;
        measurement.Value.Should().BeGreaterThanOrEqualTo(0);
        measurement.Tags["provider"].Should().Be("Ado");
        measurement.Tags["outcome"].Should().Be("succeeded");
    }

    [Fact]
    [Trait("Category", "Persistence")]
    public async Task Failed_Call_Should_Be_Recorded_As_Failed_And_Rethrown_Without_Logging_The_Exception()
    {
        // Arrange
        _inner.Setup(queries => queries.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("The database is down."));
        var queries = CreateQueries(PersistenceProvider.EfCore);

        // Act
        var act = () => queries.GetByIdAsync(Guid.NewGuid(), default);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("The database is down.");

        var log = _logger.Collector.GetSnapshot().Should().ContainSingle().Subject;
        log.Message.Should().StartWith("Persistence EfCore UserQueries.GetById failed in ");
        log.Exception.Should().BeNull("the global exception handler logs the exception once");

        OwnMeasurements().Should().ContainSingle().Which.Tags["outcome"].Should().Be("failed");
    }

    [Fact]
    [Trait("Category", "Persistence")]
    public async Task Cancelled_Call_Should_Be_Recorded_As_Cancelled()
    {
        // Arrange
        _inner.Setup(queries => queries.GetAllAsync(It.IsAny<CancellationToken>()))
              .ThrowsAsync(new OperationCanceledException());
        var queries = CreateQueries(PersistenceProvider.Ado);

        // Act
        var act = () => queries.GetAllAsync(default);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        OwnMeasurements().Should().ContainSingle().Which.Tags["outcome"].Should().Be("cancelled");
    }

    private UserQueriesInstrumentationDecorator CreateQueries(PersistenceProvider provider)
    {
        var instrumentation = new PersistenceInstrumentation(
            Options.Create(new PersistenceOptions { Provider = provider }), _logger);
        return new UserQueriesInstrumentationDecorator(_inner.Object, instrumentation);
    }

    // The meter is static, so tests running in parallel record into the same instrument:
    // keep only the measurements of the operations this class calls on its own stub.
    private IEnumerable<CollectedMeasurement<double>> OwnMeasurements() =>
        _metrics.GetMeasurementSnapshot()
                .Where(measurement => measurement.Tags.TryGetValue("operation", out var operation)
                                      && operation is "UserQueries.GetAll" or "UserQueries.GetById");
}
