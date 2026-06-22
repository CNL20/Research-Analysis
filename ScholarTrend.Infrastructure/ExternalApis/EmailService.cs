using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ScholarTrend.Application.DTOs.Common;
using ScholarTrend.Application.Interfaces.External;

namespace ScholarTrend.Infrastructure.ExternalApis;

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;
    private readonly HttpClient _httpClient;

    public EmailService(IOptions<EmailSettings> emailSettings, HttpClient httpClient)
    {
        _emailSettings = emailSettings.Value;
        _httpClient = httpClient;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        var url = "https://api.brevo.com/v3/smtp/email";
        
        var payload = new
        {
            sender = new { name = _emailSettings.SenderName, email = _emailSettings.SenderEmail },
            to = new[] { new { email = to } },
            subject = subject,
            htmlContent = body
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("api-key", _emailSettings.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("accept", "application/json");

        var response = await _httpClient.PostAsync(url, content);
        
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to send email via Brevo. Status: {response.StatusCode}. Details: {responseBody}");
        }
    }
}
