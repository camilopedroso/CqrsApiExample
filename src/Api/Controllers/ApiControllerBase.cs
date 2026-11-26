using CqrsApiExample.Api.Common.Http;
using CqrsApiExample.Domain.Common.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CqrsApiExample.Api.Controllers;

/// <summary>
///     Base for API controllers: turns <see cref="Error"/>s returned by the
///     application layer into ProblemDetails responses.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    ///     Validation errors become a 400 <see cref="ValidationProblemDetails"/>
    ///     grouped by field; otherwise the first error decides the status code.
    /// </summary>
    protected IActionResult Problem(IReadOnlyList<Error> errors)
    {
        if (errors.Count is 0)
        {
            return Problem();
        }

        // Read by CqrsApiExampleProblemDetailsFactory to add "errorCodes" to every problem response.
        HttpContext?.Items[HttpContextItemKeys.Errors] = errors;

        return errors.All(error => error.Type == ErrorType.Validation)
            ? ValidationProblem(errors)
            : (IActionResult)Problem(errors[0]);
    }

    private ObjectResult Problem(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Problem(statusCode: statusCode, title: error.Description, type: error.Code);
    }

    private ActionResult ValidationProblem(IReadOnlyList<Error> errors)
    {
        var modelStateDictionary = new ModelStateDictionary();

        foreach (var error in errors)
        {
            // ValidationProblemDetails groups messages by input field.
            modelStateDictionary.AddModelError(
                error.Field ?? error.Code,
                error.Description);
        }

        return ValidationProblem(modelStateDictionary);
    }
}
