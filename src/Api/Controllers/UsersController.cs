using CqrsApiExample.Api.Common.Mapping;
using CqrsApiExample.Application.Common.Messaging;
using CqrsApiExample.Application.Users;
using CqrsApiExample.Application.Users.Commands.CreateUser;
using CqrsApiExample.Application.Users.Queries.GetAllUsers;
using CqrsApiExample.Application.Users.Queries.GetUserById;
using CqrsApiExample.Contracts.User;
using CqrsApiExample.Domain.Common.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CqrsApiExample.Api.Controllers;

/// <summary>
///     The users endpoints (<c>/api/users</c>). Each action receives the one handler it needs
///     ([FromServices]), so its dependencies are explicit instead of hidden behind a mediator.
/// </summary>
/// <remarks>
///     Every action follows the same steps: build a query or command from the route or the
///     request contract, call the handler, then <c>Match</c> the result: success becomes a
///     response contract (via the <c>ToResponse</c> mappings), errors become a ProblemDetails
///     (<see cref="ApiControllerBase.Problem(IReadOnlyList{Error})"/>).
///     The controller never sees domain entities: handlers return read models (<see cref="UserDto"/>).
///     The handler it receives is wrapped in LoggingDecorator → AuthorizationDecorator → ValidationDecorator (see
///     <c>Application/DependencyInjection.cs</c>), so the input is already validated when the
///     handler runs. Unexpected exceptions are not caught here: GlobalExceptionHandler answers them.
///     <para>
///         <b>Why [FromServices] on the action, not the constructor.</b> [FromServices] tells model
///         binding to take that parameter from the DI container (the request's scope) instead of the
///         route, query string or body. Each action asks only for its own handler, so:
///         <list type="bullet">
///             <item>
///                 a request builds only the handler chain it uses. With constructor injection, every
///                 request would build all three handlers and their dependencies (repositories, unit
///                 of work, DbContext), even the ones the action never calls;
///             </item>
///             <item>
///                 the action's signature shows exactly which use case it runs, the job a mediator
///                 used to hide behind a generic <c>Send(request)</c>;
///             </item>
///             <item>
///                 unit tests call the action with a mocked handler as a plain argument, with no
///                 container and no constructor to fill.
///             </item>
///         </list>
///         Since ASP.NET Core 7, [ApiController] infers [FromServices] for a parameter whose type is
///         registered in DI, so the attribute could be omitted. It is kept on purpose: it makes the
///         binding source obvious and doesn't depend on a convention the reader may not know.
///     </para>
/// </remarks>
[Route("api/[controller]")]
// Reading users needs a token; registering doesn't (CreateUser opts out with [AllowAnonymous]).
// The fallback policy would require it anyway: the attribute says so where the reader looks.
[Authorize]
// Any action can fail unexpectedly; GlobalExceptionHandler answers with this 500.
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public class UsersController : ApiControllerBase
{
    /// <summary>GET /api/users: every user. Administrators only: 403 Auth.Forbidden for anyone else, 401 without a valid token.</summary>
    [HttpGet]
    [ProducesResponseType<IEnumerable<UserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> GetUsers(
        // Resolved from DI for this request, not read from the HTTP request (see the class remarks).
        [FromServices] IRequestHandler<GetAllUsersQuery, Result<List<UserDto>>> handler,
        CancellationToken cancellationToken)
    {
        GetAllUsersQuery query = new();

        Result<List<UserDto>> result = await handler.Handle(query, cancellationToken);

        return result.Match(ToOkResponse, errors => Problem(errors));

        IActionResult ToOkResponse(List<UserDto> users)
        {
            List<UserResponse> response = [.. users.Select(user => user.ToResponse())];

            return Ok(response);
        }
    }

    /// <summary>GET /api/users/{id}: one user, or 404 User.NotFound. A user may read only itself (403 Auth.Forbidden otherwise); an administrator may read anyone.</summary>
    /// <remarks>
    ///     The <c>guid</c> route constraint answers a non-GUID id with 404 Request.NotFound
    ///     before this action runs; <see cref="Guid.Empty"/> gets 400 User.InvalidId from the validator.
    /// </remarks>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetUser(
        Guid id,
        [FromServices] IRequestHandler<GetUserByIdQuery, Result<UserDto>> handler,
        CancellationToken cancellationToken)
    {
        GetUserByIdQuery query = new(id);

        Result<UserDto> result = await handler.Handle(query, cancellationToken);

        return result.Match(ToOkResponse, errors => Problem(errors));

        IActionResult ToOkResponse(UserDto user)
        {
            UserResponse response = user.ToResponse();

            return Ok(response);
        }
    }

    /// <summary>
    ///     POST /api/users: registers a user. 201 with a Location header pointing to GetUser,
    ///     400 for invalid input, 409 User.DuplicateEmail when the email is already used.
    ///     Anonymous: this is how a new user gets an account to log in with.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> CreateUser(
        CreateUserRequest request,
        [FromServices] IRequestHandler<CreateUserCommand, Result<UserDto>> handler,
        CancellationToken cancellationToken)
    {
        // The contract (Contracts project) is mapped to the command (Application), so the
        // public API and the use case can change independently.
        CreateUserCommand command = new(request.Name, request.Surname, request.Email, request.Password);

        Result<UserDto> result = await handler.Handle(command, cancellationToken);

        return result.Match(ToCreatedResponse, errors => Problem(errors));

        IActionResult ToCreatedResponse(UserDto createdUser)
        {
            // Location: /api/users/{id}, built from the GetUser route. An anonymous type, so it needs var.
            var routeValues = new { id = createdUser.Id };
            UserResponse response = createdUser.ToResponse();

            return CreatedAtAction(nameof(GetUser), routeValues, response);
        }
    }
}
