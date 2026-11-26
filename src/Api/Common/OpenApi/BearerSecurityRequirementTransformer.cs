using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CqrsApiExample.Api.Common.OpenApi;

/// <summary>
///     Marks every operation that needs a token with the bearer scheme, so Swagger UI sends the
///     token (and shows a lock) on those operations only.
/// </summary>
/// <remarks>
///     Mirrors the fallback authorization policy: an operation needs a token unless its endpoint
///     carries <see cref="IAllowAnonymous"/> (<c>[AllowAnonymous]</c> on the action or controller).
/// </remarks>
internal sealed class BearerSecurityRequirementTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation,
                               OpenApiOperationTransformerContext context,
                               CancellationToken cancellationToken)
    {
        IList<object> endpointMetadata = context.Description.ActionDescriptor.EndpointMetadata;
        bool isAnonymous = endpointMetadata.OfType<IAllowAnonymous>().Any();

        if (isAnonymous)
        {
            return Task.CompletedTask;
        }

        OpenApiSecuritySchemeReference bearerScheme = new(BearerSecuritySchemeTransformer.SchemeName, context.Document);

        // No scopes: a bearer token either is valid or isn't.
        OpenApiSecurityRequirement requirement = new()
        {
            [bearerScheme] = []
        };

        operation.Security = [requirement];

        return Task.CompletedTask;
    }
}
