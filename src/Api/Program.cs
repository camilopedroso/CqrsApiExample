using CqrsApiExample.Application;
using CqrsApiExample.Infrastructure;
using CqrsApiExample.Infrastructure.Data;

namespace CqrsApiExample.Api;

// Public so integration tests can use WebApplicationFactory<Program>.
public class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        {
            builder.Services.AddPresentation()
                            .AddApplication()
                            .AddInfrastructure()
                            .AddPersistence(builder.Configuration);
        }

        var app = builder.Build();
        {
            app.UseExceptionHandler();
            // Empty 4xx/5xx responses (unknown route, wrong method) get a ProblemDetails body.
            app.UseStatusCodePages();

            if (app.Environment.IsDevelopment())
            {
                await app.Services.InitialiseDatabaseAsync();

                // /openapi/v1.json. Anonymous: the fallback policy would otherwise require a token
                // to read the document that explains how to get one.
                app.MapOpenApi().AllowAnonymous();
                app.UseSwaggerUI(options =>          // /swagger
                    options.SwaggerEndpoint("/openapi/v1.json", "CqrsApiExample v1"));
            }

            app.UseHttpsRedirection();

            // Who is calling (reads the bearer token), then whether they may (the fallback policy
            // requires a valid token on every endpoint without [AllowAnonymous]). In this order.
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            await app.RunAsync();
        }
    }
}
