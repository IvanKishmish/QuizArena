using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using QuizArena.Application.Common.Interfaces;

namespace QuizArena.WebApi.Services;

public sealed class ResendEmailSender(HttpClient httpClient, IConfiguration configuration) : IEmailSender
{
    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var apiKey = configuration["Resend:ApiKey"]
            ?? throw new InvalidOperationException("Resend API key not found.");
        
        var fromAddress = configuration["Resend:FromAddress"]
            ?? throw new InvalidOperationException("Resend from address not found.");

        var payload = new
        {
            from = fromAddress,
            to = toEmail,
            subject,
            html = htmlBody
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}