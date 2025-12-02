using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TheJourney.Api.Modules.Mobile.Auth.Notifications;

public class MailtrapSmtpEmailSender : IEmailSender
{
    private readonly ILogger<MailtrapSmtpEmailSender> _logger;
    private readonly string? _host;
    private readonly int _port;
    private readonly string? _username;
    private readonly string? _password;
    private readonly string? _fromEmail;
    private readonly bool _isConfigured;

    public MailtrapSmtpEmailSender(IConfiguration configuration, ILogger<MailtrapSmtpEmailSender> logger)
    {
        _logger = logger;

        _host = configuration["MAILTRAP_SMTP_HOST"];
        _username = configuration["MAILTRAP_SMTP_USERNAME"];
        _password = configuration["MAILTRAP_SMTP_PASSWORD"];
        _fromEmail = configuration["MAILTRAP_FROM_EMAIL"];

        if (!int.TryParse(configuration["MAILTRAP_SMTP_PORT"], out _port))
        {
            _port = 587;
        }

        _isConfigured = !string.IsNullOrWhiteSpace(_host) &&
                        !string.IsNullOrWhiteSpace(_username) &&
                        !string.IsNullOrWhiteSpace(_password) &&
                        !string.IsNullOrWhiteSpace(_fromEmail);

        if (!_isConfigured)
        {
            _logger.LogWarning("Mailtrap SMTP sender not configured. Emails will be skipped.");
        }
    }

    public async Task SendAsync(string to, string subject, string textBody)
    {
        if (!_isConfigured)
        {
            _logger.LogInformation("Skipping email send to {Email} because Mailtrap SMTP is not configured.", to);
            return;
        }

        using var smtpClient = new SmtpClient(_host!, _port)
        {
            Credentials = new NetworkCredential(_username, _password),
            EnableSsl = true
        };

        using var message = new MailMessage(_fromEmail!, to)
        {
            Subject = subject,
            Body = textBody
        };

        try
        {
            await smtpClient.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via Mailtrap SMTP to {Email}", to);
            throw;
        }
    }
}

