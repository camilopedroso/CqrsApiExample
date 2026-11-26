namespace CqrsApiExample.Contracts.Common;

/// <summary>One page of a list. Ask for the next with <c>?page=</c> until <see cref="Page"/> reaches <see cref="TotalPages"/>.</summary>
/// <param name="Items">The items on this page.</param>
/// <param name="Page">This page's number, starting at 1.</param>
/// <param name="PageSize">The maximum number of items per page.</param>
/// <param name="TotalCount">How many items the whole list has.</param>
/// <param name="TotalPages">How many pages the list has.</param>
public record PagedResponse<T>(IReadOnlyList<T> Items,
                               int Page,
                               int PageSize,
                               int TotalCount,
                               int TotalPages);
