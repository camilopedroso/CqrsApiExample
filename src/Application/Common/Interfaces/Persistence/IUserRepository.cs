using CqrsApiExample.Domain.UserAggregate;

namespace CqrsApiExample.Application.Common.Interfaces.Persistence;

/// <summary>
///     Write side of the user feature: loads and tracks <see cref="User"/>
///     aggregates. Changes are only persisted when
///     <see cref="IUnitOfWork.SaveChangesAsync"/> is called. Reads that only
///     display data go through <see cref="IUserQueries"/>.
/// </summary>
/// <remarks>
///     <para>
///         Used only by command handlers (an architecture test forbids queries from using it).
///         It exposes only what the commands need, not a generic CRUD surface: a new command that
///         needs another operation adds it here.
///     </para>
///     <para>
///         Works with domain entities, never read models, and never saves: it only tracks, so a
///         handler can make several changes and commit them together, with their domain events,
///         in one <see cref="IUnitOfWork.SaveChangesAsync"/>.
///     </para>
///     <para>
///         Two implementations live in the Infrastructure, chosen by <c>Persistence:Provider</c>:
///         <c>UserRepository</c> (EF Core, tracks in the <c>DbContext</c>) and
///         <c>AdoUserRepository</c> (ADO.NET, tracks in the scoped <c>AdoChangeSet</c>). Both are
///         wrapped by <c>UserRepositoryInstrumentationDecorator</c>, so a new method needs both
///         implementations and the decorator.
///     </para>
/// </remarks>
public interface IUserRepository
{
    /// <summary>Whether a user with this email is already stored.</summary>
    /// <remarks>
    ///     The fast path for duplicate emails, checked before <see cref="Add" />. It can't stop two
    ///     concurrent requests with the same email; the unique index does, and the commit then
    ///     throws <c>UniqueConstraintViolationException</c>.
    /// </remarks>
    /// <param name="email">
    ///     The normalized email (trimmed and lowercase, as <c>User.Create</c> stores it), so pass
    ///     <c>user.Email</c>, not the raw input.
    /// </param>
    /// <param name="cancellationToken">The request's token.</param>
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    ///     Tracks a new user to be inserted on the next <see cref="IUnitOfWork.SaveChangesAsync"/>.
    ///     Nothing reaches the database here, which is why it is synchronous.
    /// </summary>
    /// <param name="user">A user built by <c>User.Create</c>, so its invariants already hold.</param>
    void Add(User user);
}
