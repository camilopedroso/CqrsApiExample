using CqrsApiExample.Application.Common.Events;
using CqrsApiExample.Application.Common.Interfaces.Notifications;
using CqrsApiExample.Domain.UserAggregate.Events;

namespace CqrsApiExample.Application.Users.Events;

/// <summary>Sends a welcome email to a user who was just created.</summary>
/// <remarks>
///     <para>
///         Named <c>&lt;Action&gt;When&lt;Event&gt;</c>, so the folder reads as a list of reactions.
///         It is found by assembly scanning; nothing registers it by hand.
///     </para>
///     <para>
///         Runs from the outbox, not from <c>CreateUserCommandHandler</c>: <c>User.Create</c> raises
///         <see cref="UserCreatedDomainEvent" />, the unit of work stores it with the user in the same
///         transaction, and the outbox background service calls this handler afterwards. So the
///         email goes out only for a user that was really saved, the HTTP response doesn't wait for
///         it, and a failed send is retried with backoff instead of failing the request.
///     </para>
///     <para>
///         Delivery is at-least-once: if the process stops after sending but before the outbox
///         records this handler as done, the user gets the email twice. That is acceptable for a
///         welcome email; a handler whose side effect must not repeat would need its own check.
///     </para>
///     <para>
///         Its full type name is its identity in the outbox, so renaming or moving the class makes
///         pending retries run it again.
///     </para>
/// </remarks>
public sealed class SendWelcomeEmailWhenUserCreated(IEmailSender emailSender) :
    IDomainEventHandler<UserCreatedDomainEvent>
{
    public Task Handle(UserCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        // Everything comes from the event, which carries the data the handler needs: no query to
        // load the user. The values are the ones from when the user was created, even if the
        // user changed before the outbox got to this message.
        string subject = "Welcome!";
        string body = $"Hi {domainEvent.Name}, your account was created.";

        // No try/catch: if sending throws, the outbox logs it and schedules a retry.
        // The Task is returned, not awaited, because there is nothing to do after it.
        return emailSender.SendAsync(
            domainEvent.Email,
            subject,
            body,
            cancellationToken);
    }
}
