using CqrsApiExample.Api.Common.Security;
using CqrsApiExample.Application;
using CqrsApiExample.Infrastructure;
using CqrsApiExample.Infrastructure.Data;

namespace CqrsApiExample.Api;

// Public so integration tests can use WebApplicationFactory<Program>.
public class Program
{
    // Largest request body Kestrel accepts. The API only receives small JSON documents, so
    // anything bigger is refused (413) before it is read into memory.
    private const long MaxRequestBodyBytes = 64 * 1024;

    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        {
            builder.Services.AddPresentation()
                            .AddApplication()
                            .AddInfrastructure()
                            .AddPersistence(builder.Configuration);

            // Kestrel only: the test server (WebApplicationFactory) ignores these limits.
            builder.WebHost.ConfigureKestrel(kestrel =>
            {
                // No "Server: Kestrel" header: it tells an attacker what to look up.
                kestrel.AddServerHeader = false;
                kestrel.Limits.MaxRequestBodySize = MaxRequestBodyBytes;
            });
        }

        var app = builder.Build();
        {
            app.UseExceptionHandler();
            // Empty 4xx/5xx responses (unknown route, wrong method, 401, 429) get a ProblemDetails body.
            app.UseStatusCodePages();
            // nosniff, frame denial, no referrer and a CSP on every response, errors included.
            app.UseMiddleware<SecurityHeadersMiddleware>();

            if (app.Environment.IsDevelopment())
            {
                await app.Services.InitialiseDatabaseAsync();

                // /openapi/v1.json. Anonymous: the fallback policy would otherwise require a token
                // to read the document that explains how to get one.
                app.MapOpenApi().AllowAnonymous();
                app.UseSwaggerUI(options =>          // /swagger
                    options.SwaggerEndpoint("/openapi/v1.json", "CqrsApiExample v1"));
            }
            else
            {
                // Tells browsers to use HTTPS only for this host from now on. Not in Development,
                // where the browser would remember it for localhost.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            // Before authentication: a CORS preflight carries no token and must still be answered.
            app.UseCors();

            // Who is calling (reads the bearer token), then whether they have requests left (keyed by
            // that user, or by IP), then whether they may call the endpoint (the fallback policy
            // requires a valid token on every endpoint without [AllowAnonymous]). In this order.
            app.UseAuthentication();
            app.UseRateLimiter();
            app.UseAuthorization();

            app.MapControllers();

            await app.RunAsync();
        }
    }
}
