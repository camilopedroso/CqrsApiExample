namespace CqrsApiExample.Application.Common.Paging;

/// <summary>One page of a list, and enough about the whole list to ask for the next one.</summary>
/// <remarks>
///     Lists are always paged: a request can never pull a whole table, however large it grows.
///     <see cref="TotalCount"/> comes from the same read, so a client can show "page 2 of 7".
/// </remarks>
/// <param name="Items">The items on this page, in the list's order; empty past the last page.</param>
/// <param name="Page">The page number, starting at 1.</param>
/// <param name="PageSize">The maximum number of items per page.</param>
/// <param name="TotalCount">How many items the whole list has.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    /// <summary>The first page, used when the client doesn't ask for one.</summary>
    public const int FirstPage = 1;

    /// <summary>Page size used when the client doesn't ask for one.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>The largest page a client may ask for.</summary>
    public const int MaxPageSize = 100;

    /// <summary>How many pages the list has (0 when it is empty).</summary>
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;

    /// <summary>How many items come before <paramref name="page"/>: what the database skips.</summary>
    public static int Offset(int page, int pageSize) => (page - 1) * pageSize;
}
