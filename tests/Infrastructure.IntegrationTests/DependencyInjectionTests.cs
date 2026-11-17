using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Infrastructure.Persistence;
using CqrsApiExample.Infrastructure.Persistence.Ado;
using CqrsApiExample.Infrastructure.Persistence.Decorators;
using CqrsApiExample.Infrastructure.Persistence.Queries;
using CqrsApiExample.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CqrsApiExample.Infrastructure.IntegrationTests;

public class DependencyInjectionTests
{
    [Fact]
    [Trait("Category", "Persistence")]
    public void AddPersistence_Should_Retry_Transient_SqlServer_Failures()
    {
        // Arrange: nothing connects here, so no database is needed
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = "Server=localhost;Database=Unused;Encrypt=False"
            })
            .Build();

        using var provider = new ServiceCollection().AddInfrastructure()
                                                    .AddPersistence(configuration)
                                                    .BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Act
        var strategy = context.Database.CreateExecutionStrategy();

        // Assert
        strategy.Should().BeAssignableTo<ExecutionStrategy>()
                .Which.RetriesOnFailure.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Persistence")]
    public void AddPersistence_Should_Fail_Clearly_Without_A_Connection_String()
    {
        // Arrange
        using var provider = new ServiceCollection().AddInfrastructure()
                                                    .AddPersistence(new ConfigurationBuilder().Build())
                                                    .BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Act
        var act = () => scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Model;

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("Connection string 'Database' not found.");
    }

    [Theory]
    [Trait("Category", "Persistence")]
    [InlineData(null, typeof(UserRepository), typeof(UnitOfWork), typeof(UserQueries))]
    [InlineData("EfCore", typeof(UserRepository), typeof(UnitOfWork), typeof(UserQueries))]
    [InlineData("ado", typeof(AdoUserRepository), typeof(AdoUnitOfWork), typeof(AdoUserQueries))]
    public void AddPersistence_Should_Use_The_Configured_Provider_Behind_The_Instrumentation(string? provider,
                                                                                              Type repository,
                                                                                              Type unitOfWork,
                                                                                              Type queries)
    {
        // Arrange
        using var services = BuildProvider(provider);
        using var scope = services.CreateScope();

        // Act
        var resolvedRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var resolvedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var resolvedQueries = scope.ServiceProvider.GetRequiredService<IUserQueries>();

        // Assert
        resolvedRepository.Should().BeOfType<UserRepositoryInstrumentationDecorator>().Which.Inner.Should().BeOfType(repository);
        resolvedUnitOfWork.Should().BeOfType<UnitOfWorkInstrumentationDecorator>().Which.Inner.Should().BeOfType(unitOfWork);
        resolvedQueries.Should().BeOfType<UserQueriesInstrumentationDecorator>().Which.Inner.Should().BeOfType(queries);
    }

    [Fact]
    [Trait("Category", "Persistence")]
    public void AddPersistence_Should_Batch_The_Ado_Unit_Of_Work_By_Default()
    {
        // Arrange
        using var services = BuildProvider("Ado");
        using var scope = services.CreateScope();

        // Act
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Assert
        unitOfWork.Should().BeOfType<UnitOfWorkInstrumentationDecorator>()
                  .Which.Inner.Should().BeOfType<AdoUnitOfWork>()
                  .Which.Mode.Should().Be(AdoWriteMode.Batched);
    }

    [Fact]
    [Trait("Category", "Persistence")]
    public void AddPersistence_Should_Share_The_Ado_Change_Set_Within_A_Scope_Only()
    {
        // Arrange
        using var services = BuildProvider("Ado");
        using var scope = services.CreateScope();
        using var otherScope = services.CreateScope();

        // Act
        var changes = scope.ServiceProvider.GetRequiredService<AdoChangeSet>();

        // Assert
        changes.Should().BeSameAs(scope.ServiceProvider.GetRequiredService<AdoChangeSet>());
        changes.Should().NotBeSameAs(otherScope.ServiceProvider.GetRequiredService<AdoChangeSet>());
    }

    [Fact]
    [Trait("Category", "Persistence")]
    public void AddPersistence_Should_Fail_Clearly_With_An_Unknown_Provider()
    {
        // Arrange
        using var services = BuildProvider("Dapper");
        using var scope = services.CreateScope();

        // Act
        var act = () => scope.ServiceProvider.GetRequiredService<IUserQueries>();

        // Assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("'Dapper' is not a valid Persistence:Provider. Use one of: EfCore, Ado.");
    }

    private static ServiceProvider BuildProvider(string? persistenceProvider)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = "Server=localhost;Database=Unused;Encrypt=False",
                ["Persistence:Provider"] = persistenceProvider
            })
            .Build();

        return new ServiceCollection().AddLogging()
                                      .AddInfrastructure()
                                      .AddPersistence(configuration)
                                      .BuildServiceProvider();
    }
}
