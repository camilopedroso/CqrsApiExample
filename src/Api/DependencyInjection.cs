using CqrsApiExample.Api.Common.Authentication;
using CqrsApiExample.Api.Common.Errors;
using CqrsApiExample.Api.Common.OpenApi;
using CqrsApiExample.Application.Common.Interfaces.Authentication;
using CqrsApiExample.Domain.Common.Results;
using CqrsApiExample.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CqrsApiExample.Api;

/// <summary>
///     Registers the presentation layer (ASP.NET Core): controllers, the shape of every
///     error response (ProblemDetails with <c>errorCodes</c>) and the OpenAPI document.
/// </summary>
/// <remarks>
///     Called first in <c>Program.cs</c>:
///     <c>AddPresentation().AddApplication().AddInfrastructure().AddPersistence(config)</c>.
///     The middleware that uses these services (<c>UseExceptionHandler</c>,
///     <c>UseStatusCodePages</c>, <c>MapOpenApi</c>) is configured in <c>Program.cs</c>.
/// </remarks>
public static class DependencyInjection
{
    // ProblemDetails extension that carries the error codes on every problem response.
    private const string ErrorCodesKey = "errorCodes";

    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        AddControllers(services);
        AddErrorResponses(services);
        AddAuthentication(services);

        // The OpenAPI document; Program.cs maps it (/openapi/v1.json) and the Swagger UI in Development.
        // The transformers describe the bearer token, so Swagger UI shows an "Authorize" button.
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddOperationTransformer<BearerSecurityRequirementTransformer>();
        });

        return services;
    }

    /// <summary>
    ///     JWT bearer tokens, validated with the same <c>Jwt</c> settings the Infrastructure issues
    ///     them with, and a fallback policy that makes every endpoint require a valid token unless it
    ///     says <c>[AllowAnonymous]</c>: a new endpoint is protected by default, not by remembering.
    /// </summary>
    /// <remarks>
    ///     A request without a valid token never reaches a controller: the authorization middleware
    ///     answers 401 with an empty body and a <c>WWW-Authenticate</c> header, and
    ///     <c>UseStatusCodePages</c> turns it into a ProblemDetails with <c>Request.Unauthorized</c>.
    ///     Which user may see which data is not decided here but in the Application
    ///     (<c>AuthorizationDecorator</c>), from the <see cref="ICurrentUser"/> registered below.
    /// </remarks>
    private static void AddAuthentication(IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer();

        // Configured from JwtOptions (bound and validated by AddInfrastructure) when first used,
        // so tests can change the settings after AddPresentation runs.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<JwtOptions>>(ConfigureJwtBearer);

        AuthorizationPolicy requireAuthenticatedUser = new AuthorizationPolicyBuilder()
                                                           .RequireAuthenticatedUser()
                                                           .Build();

        services.AddAuthorizationBuilder()
                .SetFallbackPolicy(requireAuthenticatedUser);

        // The Application's view of the caller (AuthorizationDecorator and the audit fields read it),
        // built from the token. A singleton: it holds no state and reads the ambient HttpContext on each
        // access, so the singleton audit interceptor can depend on it. Registered before
        // AddInfrastructure, whose anonymous fallback (TryAdd) then stays out.
        services.AddHttpContextAccessor();
        services.AddSingleton<ICurrentUser, HttpContextCurrentUser>();

        static void ConfigureJwtBearer(JwtBearerOptions bearer, IOptions<JwtOptions> jwtOptions)
        {
            JwtOptions jwt = jwtOptions.Value;
            SymmetricSecurityKey signingKey = jwt.CreateSigningKey();

            // Keeps the claim names as they are in the token ("sub"), instead of renaming them to
            // the long WS-* URIs (http://schemas.xmlsoap.org/.../nameidentifier).
            bearer.MapInboundClaims = false;

            bearer.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = signingKey,
                // Only the algorithm the issuer uses: a token signed any other way (or "none") is rejected.
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                // Tolerance for clocks that disagree a little; the default (5 min) would stretch
                // every token's lifetime by that much.
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = AccessTokenClaims.Subject,
                // ClaimsPrincipal.IsInRole (and [Authorize(Roles = ...)]) read this claim.
                RoleClaimType = AccessTokenClaims.Role
            };
        }
    }

    /// <summary>
    ///     Controllers, configured so that bad input always reaches the client in the same
    ///     ValidationProblemDetails shape, with error codes.
    /// </summary>
    private static void AddControllers(IServiceCollection services)
    {
        services.AddControllers(ConfigureMvc)
                .ConfigureApiBehaviorOptions(ConfigureApiBehavior)
                .AddJsonOptions(ConfigureJson);

        // Missing/null fields must reach the FluentValidation pipeline, which answers with the
        // domain error codes; otherwise MVC rejects non-nullable strings first, in another format.
        static void ConfigureMvc(MvcOptions options) =>
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;

        // Input MVC cannot bind at all (e.g. malformed JSON) is answered by InvalidRequestResponse,
        // in the same shape as the validation errors and with the code Request.Invalid.
        static void ConfigureApiBehavior(ApiBehaviorOptions options) =>
            options.InvalidModelStateResponseFactory = InvalidRequestResponse.Create;

        // JSON parser messages can name internal types ("could not be converted to
        // CqrsApiExample.Contracts..."); clients get a generic message under the JSON path instead.
        static void ConfigureJson(JsonOptions options) =>
            options.AllowInputFormatterExceptionMessages = false;
    }

    /// <summary>
    ///     Every error response is a ProblemDetails with <c>errorCodes</c>, whoever writes it:
    ///     a controller, the framework or the global exception handler.
    /// </summary>
    private static void AddErrorResponses(IServiceCollection services)
    {
        // Problems written by MVC (controllers, 415...): adds errorCodes and traceId.
        services.AddSingleton<ProblemDetailsFactory, CqrsApiExampleProblemDetailsFactory>();

        // Problems written outside MVC, by IProblemDetailsService (UseStatusCodePages, the exception handler).
        services.AddProblemDetails(options => options.CustomizeProblemDetails = AddRequestErrorCode);

        // Unexpected exceptions: logged once, answered with a 500 (General.Unexpected) or a 499 if cancelled.
        services.AddExceptionHandler<GlobalExceptionHandler>();
    }

    /// <summary>
    ///     Problems written outside MVC (e.g. by UseStatusCodePages for an unknown route or a
    ///     wrong method) get the matching request-level code, like every other problem.
    /// </summary>
    private static void AddRequestErrorCode(ProblemDetailsContext context)
    {
        ProblemDetails problem = context.ProblemDetails;

        // Application errors (User.*) were already added; they take precedence.
        if (problem.Extensions.ContainsKey(ErrorCodesKey))
        {
            return;
        }

        // An empty routing response has no Status yet, so the response's status code is used.
        int statusCode = problem.Status ?? context.HttpContext.Response.StatusCode;
        Error? error = RequestErrors.ForStatusCode(statusCode);

        // Only 401, 403, 404, 405 and 415 have a request-level error; any other status keeps the problem as it is.
        if (error is null)
        {
            return;
        }

        string[] errorCodes = [error.Code];
        problem.Extensions[ErrorCodesKey] = errorCodes;
    }
}
