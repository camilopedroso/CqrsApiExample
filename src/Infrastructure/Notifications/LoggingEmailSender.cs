using CqrsApiExample.Application.Common.Interfaces.Notifications;
using Microsoft.Extensions.Logging;

namespace CqrsApiExample.Infrastructure.Notifications;

/// <summary>
///     Stand-in that only logs the email (without the recipient, which is
///     personal data). Replace it with an SMTP or email-provider
///     implementation; nothing else has to change.
/// </summary>
internal sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        logger.LogInformation("Email \"{Subject}\" sent ({BodyLength} characters)", subject, body.Length);
        return Task.CompletedTask;
    }
}
