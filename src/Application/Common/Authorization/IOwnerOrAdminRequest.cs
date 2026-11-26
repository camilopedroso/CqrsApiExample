namespace CqrsApiExample.Application.Common.Authorization;

/// <summary>
///     A request about one user's data: that user may make it, and so may an administrator.
/// </summary>
/// <remarks>
///     This is resource-based authorization: the decision depends on the data the request is about,
///     not only on who is asking. <c>AuthorizationDecorator</c> compares <see cref="OwnerId"/> with the
///     caller's id and answers <c>Auth.Forbidden</c> (403) when they differ, whether or not that user
///     exists, so a 403 never confirms an id.
/// </remarks>
public interface IOwnerOrAdminRequest
{
    /// <summary>The id of the user whose data the request reads or changes.</summary>
    Guid OwnerId { get; }
}
