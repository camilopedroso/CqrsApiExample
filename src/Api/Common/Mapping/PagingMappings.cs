using CqrsApiExample.Application.Common.Paging;
using CqrsApiExample.Contracts.Common;

namespace CqrsApiExample.Api.Common.Mapping;

public static class PagingMappings
{
    /// <summary>Maps a page of read models to a page of contracts, item by item.</summary>
    public static PagedResponse<TResponse> ToResponse<TItem, TResponse>(this PagedResult<TItem> page,
                                                                        Func<TItem, TResponse> mapItem)
    {
        List<TResponse> items = [.. page.Items.Select(mapItem)];

        PagedResponse<TResponse> response = new(items, page.Page, page.PageSize, page.TotalCount, page.TotalPages);

        return response;
    }
}
