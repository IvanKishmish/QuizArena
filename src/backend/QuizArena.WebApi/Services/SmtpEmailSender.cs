using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using QuizArena.Application.Common.Interfaces;
using QuizArena.WebApi.Options;

namespace QuizArena.WebApi.Services;

public sealed class SmtpEmailSender(IOptions<SmtpOptions> smtpOptions, ILogger<SmtpEmailSender> logger)
: IEmailSender
{
    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var options = smtpOptions.Value;
        var fromAddress = string.IsNullOrWhiteSpace(options.FromAddress) ? options.Username : options.FromAddress;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName, fromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync(options.Host, options.Port, SecureSocketOptions.StartTls, ct);
            await client.AuthenticateAsync(options.Username, options.Password, ct);
            await client.SendAsync(message, ct);

            logger.LogInformation("Email sent to {ToEmail} via SMTP ({Host}:{Port})", toEmail, options.Host, options.Port);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {ToEmail} via SMTP ({Host}:{Port})", toEmail, options.Host, options.Port);
            throw;
        }
        finally
        {
            if (client.IsConnected)
                await client.DisconnectAsync(true, ct);
        }
    }
}
