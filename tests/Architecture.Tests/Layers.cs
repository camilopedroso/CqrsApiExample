using System.Reflection;
using CqrsApiExample.Contracts.User;
using CqrsApiExample.Domain.Common.Models;

namespace CqrsApiExample.Architecture.Tests;

internal static class Layers
{
    public static readonly Assembly Domain = typeof(Entity).Assembly;
    public static readonly Assembly Application = typeof(CqrsApiExample.Application.DependencyInjection).Assembly;
    public static readonly Assembly Infrastructure = typeof(CqrsApiExample.Infrastructure.DependencyInjection).Assembly;
    public static readonly Assembly Api = typeof(CqrsApiExample.Api.DependencyInjection).Assembly;
    public static readonly Assembly Contracts = typeof(UserResponse).Assembly;
}
