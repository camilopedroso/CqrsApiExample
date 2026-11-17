using CqrsApiExample.Application.Common.Messaging;

namespace CqrsApiExample.Application.UnitTests.Decorators;

/// <summary>Inner handler for decorator tests: returns a fixed result and counts calls.</summary>
internal sealed class StubHandler<TRequest, TResult>(TResult result) : IRequestHandler<TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    public int Calls { get; private set; }

    public Task<TResult> Handle(TRequest request, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(result);
    }
}
