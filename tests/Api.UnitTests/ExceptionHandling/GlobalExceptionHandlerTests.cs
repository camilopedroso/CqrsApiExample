using CqrsApiExample.Api.Common.Errors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using DomainErrors = CqrsApiExample.Domain.Common.DomainErrors.Errors;

namespace CqrsApiExample.Api.UnitTests.ExceptionHandling;

public class GlobalExceptionHandlerTests
{
    private readonly Mock<IProblemDetailsService> _problemDetailsService = new();
    private readonly FakeLogger<GlobalExceptionHandler> _logger = new();
    private ProblemDetailsContext? _writtenProblem;

    public GlobalExceptionHandlerTests()
    {
        _problemDetailsService.Setup(x => x.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
                              .Callback<ProblemDetailsContext>(context => _writtenProblem = context)
                              .ReturnsAsync(true);
    }

    [Theory]
    [Trait("Category", "ExceptionHandler")]
    [InlineData("Development", "Boom")]
    [InlineData("Production", null)]
    public async Task TryHandleAsync_Should_Log_Error_And_Write_500_ProblemDetails(string environment,
                                                                                  string? expectedDetail)
    {
        // Arrange
        var handler = CreateHandler(environment);
        var httpContext = new DefaultHttpContext();
        var exception = new InvalidOperationException("Boom");

        // Act
        var handled = await handler.TryHandleAsync(httpContext, exception, default);

        // Assert
        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        _writtenProblem.Should().NotBeNull();
        _writtenProblem!.ProblemDetails.Status.Should().Be(StatusCodes.Status500InternalServerError);
        _writtenProblem.ProblemDetails.Type.Should().Be(DomainErrors.Generic.Ops.Code);
        _writtenProblem.ProblemDetails.Title.Should().Be(DomainErrors.Generic.Ops.Description);
        _writtenProblem.ProblemDetails.Detail.Should().Be(expectedDetail);
        _logger.Collector.LatestRecord.Level.Should().Be(LogLevel.Error);
        _logger.Collector.LatestRecord.Exception.Should().BeSameAs(exception);
    }

    [Fact]
    [Trait("Category", "ExceptionHandler")]
    public async Task TryHandleAsync_Should_Answer_499_Without_Error_Log_When_Client_Cancelled()
    {
        // Arrange
        var handler = CreateHandler("Production");
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();
        var httpContext = new DefaultHttpContext { RequestAborted = aborted.Token };

        // Act
        var handled = await handler.TryHandleAsync(httpContext, new OperationCanceledException(), default);

        // Assert
        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(GlobalExceptionHandler.StatusClientClosedRequest);
        _writtenProblem.Should().BeNull();
        _logger.Collector.GetSnapshot().Should().OnlyContain(record => record.Level == LogLevel.Information);
    }

    private GlobalExceptionHandler CreateHandler(string environmentName)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns(environmentName);
        return new GlobalExceptionHandler(_problemDetailsService.Object, environment.Object, _logger);
    }
}
