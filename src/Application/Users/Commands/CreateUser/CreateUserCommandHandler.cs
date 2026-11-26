using CqrsApiExample.Application.Common.Exceptions;
using CqrsApiExample.Application.Common.Interfaces.Authentication;
using CqrsApiExample.Application.Common.Interfaces.Persistence;
using CqrsApiExample.Application.Common.Messaging;
using CqrsApiExample.Domain.Common.DomainErrors;
using CqrsApiExample.Domain.Common.Results;
using CqrsApiExample.Domain.UserAggregate;

namespace CqrsApiExample.Application.Users.Commands.CreateUser;

/// <summary>
///     Creates a user and stores it, together with its <c>UserCreatedDomainEvent</c>,
///     in one transaction.
/// </summary>
/// <remarks>
///     <para>
///         Callers never get this class directly: they receive it wrapped in
///         <c>LoggingDecorator</c> → <c>ValidationDecorator</c>, so by the time
///         <see cref="Handle"/> runs, <c>CreateUserCommandValidator</c> has already checked
///         the input format. Expected failures (invalid data, duplicate email) are returned
///         as errors; any other exception propagates to the API's global exception handler.
///     </para>
///     <para>
///         A command handler: it writes through <see cref="IUserRepository" /> and commits with
///         <see cref="IUnitOfWork" />, both scoped to the request. Neither knows whether EF Core or
///         ADO.NET is behind it (<c>Persistence:Provider</c>).
///     </para>
///     <para>
///         The controller turns the result into HTTP: a <see cref="UserDto" /> becomes 201 Created,
///         <c>User.Invalid*</c> errors 400 and <c>User.DuplicateEmail</c> 409.
///     </para>
/// </remarks>
public sealed class CreateUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher) : IRequestHandler<CreateUserCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(
        CreateUserCommand request,
        CancellationToken cancellationToken)
    {
        // The validator already checked the password rules. Only the hash goes any further:
        // the domain, the database and the logs never see the password itself.
        string passwordHash = passwordHasher.Hash(request.Password);

        // The factory enforces the domain invariants (they repeat the validator's checks,
        // with the same error codes) and raises UserCreatedDomainEvent inside the aggregate.
        Result<User> userResult = User.Create(request.Name, request.Surname, request.Email, passwordHash);

        if (userResult.IsError)
        {
            // Passes the domain errors on unchanged. Explicit because Errors is an
            // IReadOnlyList<Error>, and Result<T> only converts implicitly from Error,
            // List<Error> and the value.
            return Result<UserDto>.Failure(userResult.Errors);
        }

        // Safe: the result was checked above (Value throws on a failed result).
        User user = userResult.Value;

        // Fast path for the common case; the unique index covers concurrent requests.
        bool emailAlreadyUsed = await userRepository.ExistsByEmailAsync(user.Email, cancellationToken);

        if (emailAlreadyUsed)
        {
            // An Error converts implicitly to a failed Result<UserDto>.
            return Errors.Users.DuplicateEmail;
        }

        // Only tracks the user: repositories never save. The commit below is explicit.
        userRepository.Add(user);

        try
        {
            // Also writes UserCreatedDomainEvent (raised by User.Create) to the outbox, in the same
            // transaction as the user. Nothing is published here: the outbox background service
            // delivers the event to its handlers later, so the response doesn't wait for them.
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            // Another request saved the same email between the check above and this commit.
            // The transaction was rolled back, so the outbox message was not stored either.
            // The email index is the only unique one on Users; a new unique column would need
            // to tell the violations apart here.
            return Errors.Users.DuplicateEmail;
        }

        // Commands return read models, never the domain entity. Built after the commit, so it
        // carries what was actually stored (the trimmed name, the lowercase email).
        UserDto userDto = UserDto.From(user);

        return userDto;
    }
}
