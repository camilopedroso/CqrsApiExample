using CqrsApiExample.Application.Common.Messaging;
using CqrsApiExample.Domain.Common.Results;
using FluentValidation;
using FluentValidation.Results;

namespace CqrsApiExample.Application.Common.Decorators;

/// <summary>
///     Runs the request's validator, if there is one, and returns its errors
///     as a failed result instead of calling the handler.
/// </summary>
/// <remarks>
///     <para>
///         It is the innermost decorator
///         (<c>LoggingDecorator → AuthorizationDecorator → ValidationDecorator → handler</c>,
///         built by <c>AddRequestHandlers</c>), so an invalid request never reaches the handler,
///         the logging decorator still records it as a failure with its error codes, and only a
///         caller allowed to make the request gets to see its validation errors.
///     </para>
///     <para>
///         Invalid input is an expected failure, so it is returned as a result, not thrown.
///         The constraint <c>TResult : IFailureResult&lt;TResult&gt;</c> is what makes that
///         possible in generic code: <c>TResult.Failure(errors)</c> is a static abstract member,
///         so the decorator builds a failed <c>Result&lt;T&gt;</c> without knowing <c>T</c>
///         (no exception, no reflection, no <c>dynamic</c>).
///     </para>
///     <para>
///         This only checks the input format. The entity factories (such as <c>User.Create</c>)
///         still protect the domain invariants; both use the same error codes.
///     </para>
/// </remarks>
public sealed class ValidationDecorator<TRequest, TResult>(
    IRequestHandler<TRequest, TResult> inner,
    IValidator<TRequest>? validator = null) : IRequestHandler<TRequest, TResult>
                                                where TRequest : IRequest<TResult>
                                                where TResult : IFailureResult<TResult>
{
    // The validator is optional: validators are found by assembly scanning, and a request
    // without one has nothing registered. ActivatorUtilities then
    // falls back to the default value, null, instead of failing.

    public async Task<TResult> Handle(TRequest request, CancellationToken cancellationToken)
    {
        // No validator for this request: nothing to check, go straight to the handler.
        if (validator is null)
        {
            return await inner.Handle(request, cancellationToken);
        }

        // Runs every rule. Validators with several checks per field set
        // RuleLevelCascadeMode = Stop, so there is at most one failure per field.
        ValidationResult validationResult = await validator.ValidateAsync(request, cancellationToken);

        if (validationResult.IsValid)
        {
            return await inner.Handle(request, cancellationToken);
        }

        // Invalid: the handler is skipped and the failures become domain Errors.
        List<Error> errors = validationResult.Errors.ConvertAll(ToError);

        // Returned as a failed result; the controller turns it into a 400 ValidationProblemDetails.
        return TResult.Failure(errors);

        // Rules set ErrorCode with WithErrorCode (Area.Name, the same code the entity factory uses);
        // the property name is kept separately as the field, which the 400 response groups by.
        static Error ToError(ValidationFailure validationFailure)
        {
            Error error = Error.Validation(
                validationFailure.ErrorCode,
                validationFailure.ErrorMessage,
                validationFailure.PropertyName);

            return error;
        }
    }
}
