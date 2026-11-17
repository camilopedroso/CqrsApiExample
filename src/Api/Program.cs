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

                app.MapOpenApi();                    // /openapi/v1.json
                app.UseSwaggerUI(options =>          // /swagger
                    options.SwaggerEndpoint("/openapi/v1.json", "CqrsApiExample v1"));
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            await app.RunAsync();
        }
    }
}
