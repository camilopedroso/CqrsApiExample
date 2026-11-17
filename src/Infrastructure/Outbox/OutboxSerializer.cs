using System.Text.Json;
using CqrsApiExample.Domain.Common.Models;

namespace CqrsApiExample.Infrastructure.Outbox;

/// <summary>
///     Stores domain events as (type name, JSON). Types are resolved only from
///     the known domain events, never from an arbitrary type name in the
///     database. Renaming or moving an event type breaks pending messages of
///     the old name, so treat event names and shapes as a contract.
/// </summary>
internal static class OutboxSerializer
{
    private static readonly Dictionary<string, Type> KnownEventTypes =
        typeof(IDomainEvent).Assembly.GetTypes()
                            .Where(type => type is { IsClass: true, IsAbstract: false }
                                           && typeof(IDomainEvent).IsAssignableFrom(type))
                            .ToDictionary(type => type.FullName!);

    public static (string Type, string Content) Serialize(IDomainEvent domainEvent)
    {
        var type = domainEvent.GetType();
        return (type.FullName!, JsonSerializer.Serialize(domainEvent, type));
    }

    public static IDomainEvent Deserialize(string typeName, string content)
    {
        if (!KnownEventTypes.TryGetValue(typeName, out var type))
        {
            throw new InvalidOperationException($"Unknown domain event type '{typeName}'.");
        }

        return (IDomainEvent)(JsonSerializer.Deserialize(content, type)
                              ?? throw new InvalidOperationException($"Empty content for '{typeName}'."));
    }
}
