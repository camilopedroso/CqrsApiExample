namespace CqrsApiExample.Domain.Common.Models;

/// <summary>
///     Base class for entities: two entities are equal when they have the same
///     type and the same identity, regardless of the state of their properties.
/// </summary>
public abstract class Entity : IEquatable<Entity>
{
    protected Entity()
    {
        Id = Guid.CreateVersion7();
    }

    public Guid Id { get; private init; }

    // Audit fields are filled by the persistence layer, never by the domain.
    public DateTime CreatedAt { get; private set; }
    public DateTime? ModifiedAt { get; private set; }

    public bool Equals(Entity? other)
    {
        return other is not null
            && other.GetType() == GetType()
            && Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Entity);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public static bool operator ==(Entity? left, Entity? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Entity? left, Entity? right)
    {
        return !Equals(left, right);
    }
}
