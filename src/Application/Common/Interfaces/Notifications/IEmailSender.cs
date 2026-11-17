namespace CqrsApiExample.Application.Common.Interfaces.Notifications;

/// <summary>
///     Sends an email. The Application only depends on this abstraction; how the email
///     leaves the system (SMTP, an email provider's API...) is an Infrastructure detail.
/// </summary>
/// <remarks>
///     <para>
///         Today it is implemented by <c>LoggingEmailSender</c> (Infrastructure), a stand-in that
///         only logs the subject and the body length. A real implementation replaces it in
///         <c>AddInfrastructure</c>, and nothing in the Application changes. The integration tests
///         swap it for a <c>FakeEmailSender</c> that records what was sent.
///     </para>
///     <para>
///         It is called from domain event handlers (<c>SendWelcomeEmailWhenUserCreated</c>), which
///         run from the outbox, never from a request handler: the email goes out only after the
///         change was committed, and a failure is retried instead of failing the HTTP request.
///         Because the outbox delivers at least once, the same email can be sent twice.
///     </para>
///     <para>
///         An implementation should throw when sending fails, so the outbox retries it, and should
///         not log the recipient, which is personal data.
///     </para>
/// </remarks>
public interface IEmailSender
{
    /// <summary>Sends one email.</summary>
    /// <param name="to">The recipient's address. Personal data: don't log it.</param>
    /// <param name="subject">The subject line.</param>
    /// <param name="body">The message body.</param>
    /// <param name="cancellationToken">Cancelled when the application is shutting down.</param>
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken);
}
