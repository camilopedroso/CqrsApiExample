using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CqrsApiExample.Api.Common.OpenApi;

/// <summary>
///     Declares the bearer token scheme in the OpenAPI document (<c>components.securitySchemes</c>),
///     which is what gives Swagger UI its "Authorize" button.
/// </summary>
/// <remarks>
///     It only declares the scheme. Which operations need it is said by
///     <see cref="BearerSecurityRequirementTransformer"/>, operation by operation.
/// </remarks>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    /// <summary>The scheme's name in the document; the operations refer to it by this name.</summary>
    public const string SchemeName = "Bearer";

    public Task TransformAsync(OpenApiDocument document,
                               OpenApiDocumentTransformerContext context,
                               CancellationToken cancellationToken)
    {
        OpenApiSecurityScheme bearerScheme = new()
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access token from POST /api/auth/login."
        };

        document.AddComponent<IOpenApiSecurityScheme>(SchemeName, bearerScheme);

        return Task.CompletedTask;
    }
}
