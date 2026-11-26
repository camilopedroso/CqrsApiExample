namespace CqrsApiExample.Api.Common.Security;

/// <summary>
///     Adds the response headers that tell browsers to be strict with this API's responses.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item><c>X-Content-Type-Options: nosniff</c>: never guess a content type other than the one declared.</item>
///         <item><c>X-Frame-Options: DENY</c> and CSP <c>frame-ancestors 'none'</c>: never show a response inside a frame (clickjacking).</item>
///         <item><c>Referrer-Policy: no-referrer</c>: links followed from a response don't reveal its URL.</item>
///         <item>
///             <c>Content-Security-Policy: default-src 'none'</c>: a JSON response loads nothing, so if one is ever
///             opened as a page, no script in it can run. Not on <c>/swagger</c>, whose page needs its scripts.
///         </item>
///     </list>
///     The headers are added when the response starts (<c>OnStarting</c>), not when the request comes
///     in: the exception handler clears the headers before writing a 500, and these must survive it.
///     HSTS is not here: <c>UseHsts</c> adds it outside Development.
/// </remarks>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(AddHeaders);

        return next(context);

        Task AddHeaders()
        {
            IHeaderDictionary headers = context.Response.Headers;

            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";

            bool isSwaggerUi = context.Request.Path.StartsWithSegments("/swagger");

            if (!isSwaggerUi)
            {
                headers.ContentSecurityPolicy = ApiContentSecurityPolicy;
            }

            return Task.CompletedTask;
        }
    }
}
