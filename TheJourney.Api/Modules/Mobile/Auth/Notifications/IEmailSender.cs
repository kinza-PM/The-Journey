namespace TheJourney.Api.Modules.Mobile.Auth.Notifications;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string textBody);
}

