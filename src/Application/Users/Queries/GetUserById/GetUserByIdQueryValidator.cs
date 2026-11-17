using CqrsApiExample.Domain.Common.DomainErrors;
using FluentValidation;

namespace CqrsApiExample.Application.Users.Queries.GetUserById;

public sealed class GetUserByIdQueryValidator : AbstractValidator<GetUserByIdQuery>
{
    public GetUserByIdQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty()
                          .WithErrorCode(Errors.Users.InvalidId.Code)
                          .WithMessage(Errors.Users.InvalidId.Description);
    }
}
