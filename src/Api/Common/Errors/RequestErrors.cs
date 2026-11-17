using CqrsApiExample.Domain.Common.Results;

namespace CqrsApiExample.Api.Common.Errors;

/// <summary>
///     Errors about the HTTP request itself, raised before any application
///     code runs (unbindable body, unknown route, wrong method or media type).
/// </summary>
public static class RequestErrors
{
    public static Error Invalid =>
        Error.Validation(code: "Request.Invalid", description: "The request could not be read.");

    public static Error NotFound =>
        Error.NotFound(code: "Request.NotFound", description: "No endpoint matches the request path.");

    public static Error MethodNotAllowed =>
        Error.Failure(code: "Request.MethodNotAllowed", description: "The HTTP method is not supported by this endpoint.");

    public static Error UnsupportedMediaType =>
        Error.Failure(code: "Request.UnsupportedMediaType", description: "The request content type is not supported.");

    /// <summary>
    ///     Code for a problem response that the framework produced without an
    ///     application error, based on its status code.
    /// </summary>
    public static Error? ForStatusCode(int statusCode) => statusCode switch
    {
        StatusCodes.Status404NotFound => NotFound,
        StatusCodes.Status405MethodNotAllowed => MethodNotAllowed,
        StatusCodes.Status415UnsupportedMediaType => UnsupportedMediaType,
        _ => null
    };
}
