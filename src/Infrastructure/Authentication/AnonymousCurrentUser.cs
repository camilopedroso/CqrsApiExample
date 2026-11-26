using CqrsApiExample.Application.Common.Interfaces.Authentication;

namespace CqrsApiExample.Infrastructure.Authentication;

/// <summary>
///     The <see cref="ICurrentUser"/> when there are no requests at all: the benchmarks, tools
///     and tests that use the Infrastructure without the API. Nobody is ever signed in.
/// </summary>
/// <remarks>
///     <c>AddInfrastructure</c> registers it with <c>TryAdd</c>, so the API's own implementation
///     (registered first, by <c>AddPresentation</c>) wins whenever there is one.
/// </remarks>
internal sealed class AnonymousCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => false;

    public Guid? Id => null;

    public bool IsInRole(string role) => false;
}
