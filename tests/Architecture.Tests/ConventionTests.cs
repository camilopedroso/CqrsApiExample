using System.Reflection;
using CqrsApiExample.Application.Common.Interfaces.Authentication;
using CqrsApiExample.Application.Common.Interfaces.Notifications;
using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Application.Common.Messaging;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;

namespace CqrsApiExample.Architecture.Tests;

public class ConventionTests
{
    [Fact]
    [Trait("Category", "Architecture")]
    public void Queries_Should_Not_Use_The_Write_Side_Repository()
    {
        Types.InAssembly(Layers.Application)
             .That().ResideInNamespaceContaining(".Queries")
             .ShouldNot().HaveDependencyOnAny(typeof(IUserRepository).FullName!)
             .GetResult()
             .ShouldPass("queries read through I*Queries projections, not through aggregates (CQRS)");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Handlers_And_Validators_Should_Be_Sealed()
    {
        Types.InAssembly(Layers.Application)
             .That().AreClasses().And().HaveNameEndingWith("Handler")
             .Should().BeSealed()
             .GetResult()
             .ShouldPass("handlers are leaves, not extension points");

        Types.InAssembly(Layers.Application)
             .That().AreClasses().And().HaveNameEndingWith("Validator")
             .Should().BeSealed()
             .GetResult()
             .ShouldPass("validators are leaves, not extension points");
    }

    [Theory]
    [Trait("Category", "Architecture")]
    [InlineData(typeof(IUserRepository))]
    [InlineData(typeof(IUserQueries))]
    [InlineData(typeof(IUnitOfWork))]
    [InlineData(typeof(IEmailSender))]
    [InlineData(typeof(IPasswordHasher))]
    [InlineData(typeof(IAccessTokenIssuer))]
    public void Infrastructure_Implementations_Should_Be_Internal(Type abstraction)
    {
        var implementations = Types.InAssembly(Layers.Infrastructure).That().ImplementInterface(abstraction);

        implementations.GetTypes().Should().NotBeEmpty($"{abstraction.Name} must be implemented by the infrastructure");
        implementations.Should().NotBePublic()
                       .GetResult()
                       .ShouldPass("consumers depend on the application abstraction, never on the implementation");
    }
    [Fact]
    [Trait("Category", "Architecture")]
    public void Every_Controller_Action_Should_Declare_Whether_It_Needs_A_Token()
    {
        // The fallback policy already protects everything; this makes the choice visible in the
        // code, so an anonymous endpoint is always a deliberate [AllowAnonymous].
        var actions = Layers.Api.GetTypes()
                            .Where(type => type.IsSubclassOf(typeof(ControllerBase)) && !type.IsAbstract)
                            .SelectMany(controller => controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                                                                .Select(action => (controller, action)))
                            .ToList();

        actions.Should().NotBeEmpty();
        var undeclared = actions.Where(pair => !DeclaresAccess(pair.action) && !DeclaresAccess(pair.controller))
                                .Select(pair => $"{pair.controller.Name}.{pair.action.Name}");
        undeclared.Should().BeEmpty("every action (or its controller) says [Authorize] or [AllowAnonymous]");

        static bool DeclaresAccess(MemberInfo member) =>
            member.GetCustomAttributes(inherit: true).Any(attribute => attribute is IAuthorizeData or IAllowAnonymous);
    }

    [Theory]
    [Trait("Category", "Architecture")]
    [InlineData(typeof(ICommand<>), ".Commands.", "Command")]
    [InlineData(typeof(IQuery<>), ".Queries.", "Query")]
    public void Requests_Should_Live_On_Their_Side_Of_CQRS(Type requestInterface, string expectedNamespacePart, string expectedSuffix)
    {
        var requests = Layers.Application.GetTypes()
                             .Where(type => !type.IsInterface
                                            && type.GetInterfaces().Any(@interface => @interface.IsGenericType
                                                && @interface.GetGenericTypeDefinition() == requestInterface))
                             .ToList();

        requests.Should().NotBeEmpty();
        requests.Should().OnlyContain(type => type.Namespace!.Contains(expectedNamespacePart) && type.Name.EndsWith(expectedSuffix),
                                      $"every {requestInterface.Name} belongs in a {expectedNamespacePart} namespace and is named *{expectedSuffix}");
    }
}
