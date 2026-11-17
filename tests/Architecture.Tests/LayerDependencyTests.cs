using FluentAssertions;
using NetArchTest.Rules;

namespace CqrsApiExample.Architecture.Tests;

/// <summary>
///     Dependencies point inward: Domain ← Application ← Infrastructure / API.
///     These rules read the compiled IL, so a forbidden dependency fails the
///     build pipeline even if it compiles.
/// </summary>
public class LayerDependencyTests
{
    [Fact]
    [Trait("Category", "Architecture")]
    public void Domain_Should_Not_Know_About_Persistence_Or_Frameworks()
    {
        Types.InAssembly(Layers.Domain)
             .ShouldNot()
             .HaveDependencyOnAny("Microsoft.EntityFrameworkCore",
                                  "System.ComponentModel.DataAnnotations",
                                  "MediatR",
                                  "FluentValidation",
                                  "Microsoft.AspNetCore",
                                  "Microsoft.Extensions")
             .GetResult()
             .ShouldPass("the domain must be persistence-ignorant and framework-free");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Domain_Should_Not_Depend_On_Other_Layers()
    {
        Types.InAssembly(Layers.Domain)
             .ShouldNot()
             .HaveDependencyOnAny("CqrsApiExample.Application", "CqrsApiExample.Infrastructure", "CqrsApiExample.Api", "CqrsApiExample.Contracts")
             .GetResult()
             .ShouldPass("the domain is the innermost layer");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Domain_Assembly_Should_Reference_Only_The_Base_Class_Library()
    {
        Layers.Domain.GetReferencedAssemblies()
              .Select(reference => reference.Name)
              .Should().OnlyContain(name => name!.StartsWith("System"),
                                    "the domain project must not reference any package or project");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void No_Layer_Should_Depend_On_MediatR()
    {
        foreach (var layer in new[] { Layers.Domain, Layers.Application, Layers.Infrastructure, Layers.Api, Layers.Contracts })
        {
            Types.InAssembly(layer)
                 .ShouldNot()
                 .HaveDependencyOnAny("MediatR")
                 .GetResult()
                 .ShouldPass($"{layer.GetName().Name} uses the project's own request handlers and decorators");
        }
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Application_Should_Not_Depend_On_Infrastructure_Api_Or_Data_Access()
    {
        Types.InAssembly(Layers.Application)
             .ShouldNot()
             .HaveDependencyOnAny("CqrsApiExample.Infrastructure",
                                  "CqrsApiExample.Api",
                                  "CqrsApiExample.Contracts",
                                  "Microsoft.EntityFrameworkCore",
                                  "Microsoft.Data.SqlClient",
                                  "Microsoft.AspNetCore")
             .GetResult()
             .ShouldPass("the application defines abstractions and the outer layers implement them");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Infrastructure_Should_Not_Depend_On_Api_Or_Web()
    {
        Types.InAssembly(Layers.Infrastructure)
             .ShouldNot()
             .HaveDependencyOnAny("CqrsApiExample.Api", "CqrsApiExample.Contracts", "Microsoft.AspNetCore")
             .GetResult()
             .ShouldPass("the infrastructure must not depend on the presentation layer");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Contracts_Should_Not_Depend_On_Any_Layer()
    {
        Types.InAssembly(Layers.Contracts)
             .ShouldNot()
             .HaveDependencyOnAny("CqrsApiExample.Domain", "CqrsApiExample.Application", "CqrsApiExample.Infrastructure", "CqrsApiExample.Api")
             .GetResult()
             .ShouldPass("the HTTP contracts are plain DTOs shared with API clients");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Api_Should_Not_Use_Domain_Entities_Or_Data_Access()
    {
        Types.InAssembly(Layers.Api)
             .ShouldNot()
             .HaveDependencyOnAny("CqrsApiExample.Domain.UserAggregate",
                                  "CqrsApiExample.Infrastructure.Persistence",
                                  "Microsoft.EntityFrameworkCore")
             .GetResult()
             .ShouldPass("the API works with read models and contracts; entities stay in the application layer");
    }
}
