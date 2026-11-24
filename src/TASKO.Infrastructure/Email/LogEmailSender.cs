using Microsoft.Extensions.Logging;
using Tasko.Application.Abstractions;

namespace Tasko.Infrastructure.Email;

/// <summary>
/// Provedor de e-mail do MVP: registra no log. Substituir por SMTP/SendGrid em produção.
/// </summary>
public sealed class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        logger.LogInformation("E-mail para {Recipient}: {Subject}", to, subject);
        logger.LogDebug("Conteúdo do e-mail: {Body}", body);
        return Task.CompletedTask;
    }
}
