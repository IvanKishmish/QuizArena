using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using QuizArena.Application.Common.Interfaces;

namespace QuizArena.WebApi.Services;

public sealed class SmtpEmailSender
: IEmailSender
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly string _fromAddress;
    private readonly string _fromName;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _logger = logger;
        _host = configuration["Smtp:Host"] ?? throw new InvalidOperationException("SMTP host not found.");
        _port = int.Parse(configuration["Smtp:Port"] ?? throw new InvalidOperationException("SMTP port not found."));
        _username = configuration["Smtp:Username"] ?? throw new InvalidOperationException("SMTP username not found.");
        _password = configuration["Smtp:Password"] ?? throw new InvalidOperationException("SMTP password not found.");
        _fromAddress = configuration["Smtp:FromAddress"] ?? _username;
        _fromName = configuration["Smtp:FromName"] ?? "QuizArena";
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_fromName, _fromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync(_host, _port, SecureSocketOptions.StartTls, ct);
            await client.AuthenticateAsync(_username, _password, ct);
            await client.SendAsync(message, ct);

            _logger.LogInformation("Email sent to {ToEmail} via SMTP ({Host}:{Port})", toEmail, _host, _port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail} via SMTP ({Host}:{Port})", toEmail, _host, _port);
            throw;
        }
        finally
        {
            if (client.IsConnected)
                await client.DisconnectAsync(true, ct);
        }
    }
}