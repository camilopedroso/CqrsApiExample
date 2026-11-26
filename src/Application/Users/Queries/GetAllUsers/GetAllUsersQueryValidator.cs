using CqrsApiExample.Application.Common.Paging;
using CqrsApiExample.Domain.Common.DomainErrors;
using FluentValidation;

namespace CqrsApiExample.Application.Users.Queries.GetAllUsers;

/// <summary>
///     Keeps paging inside its limits: page 1 or later, and at most
///     <see cref="PagedResult{T}.MaxPageSize" /> users per page.
/// </summary>
public sealed class GetAllUsersQueryValidator : AbstractValidator<GetAllUsersQuery>
{
    public GetAllUsersQueryValidator()
    {
        const int maxPageSize = PagedResult<UserDto>.MaxPageSize;

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(PagedResult<UserDto>.FirstPage)
            .WithErrorCode(Errors.Paging.InvalidPage.Code)
            .WithMessage(Errors.Paging.InvalidPage.Description);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, maxPageSize)
            .WithErrorCode(Errors.Paging.InvalidPageSize(maxPageSize).Code)
            .WithMessage(Errors.Paging.InvalidPageSize(maxPageSize).Description);
    }
}
