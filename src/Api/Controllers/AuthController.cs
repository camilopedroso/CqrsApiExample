using CqrsApiExample.Api.Common.Mapping;
using CqrsApiExample.Application.Auth;
using CqrsApiExample.Application.Auth.Commands.Login;
using CqrsApiExample.Application.Common.Messaging;
using CqrsApiExample.Contracts.Auth;
using CqrsApiExample.Domain.Common.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CqrsApiExample.Api.Controllers;

/// <summary>
///     Authentication endpoints (<c>/api/auth</c>). Anonymous: they are how a client gets a token.
/// </summary>
/// <remarks>
///     Every other endpoint requires a token (the fallback authorization policy in
///     <c>AddPresentation</c>), so this controller opts out explicitly with
///     <see cref="AllowAnonymousAttribute"/>. An architecture test checks that every action
///     says whether it is anonymous or protected.
/// </remarks>
[Route("api/[controller]")]
[AllowAnonymous]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public class AuthController : ApiControllerBase
{
    /// <summary>
    ///     POST /api/auth/login: exchanges an email and password for an access token.
    ///     401 Auth.InvalidCredentials for an unknown email or a wrong password (the same answer
    ///     for both), 400 when a field is missing.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType<AccessTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> Login(
        LoginRequest request,
        [FromServices] IRequestHandler<LoginCommand, Result<AccessTokenDto>> handler,
        CancellationToken cancellationToken)
    {
        LoginCommand command = new(request.Email, request.Password);

        Result<AccessTokenDto> result = await handler.Handle(command, cancellationToken);

        return result.Match(ToOkResponse, errors => Problem(errors));

        IActionResult ToOkResponse(AccessTokenDto accessToken)
        {
            AccessTokenResponse response = accessToken.ToResponse();

            // A token is a credential: no browser or proxy may keep a copy (RFC 6749, 5.1).
            HttpContext?.Response.Headers.CacheControl = "no-store";

            return Ok(response);
        }
    }
}
