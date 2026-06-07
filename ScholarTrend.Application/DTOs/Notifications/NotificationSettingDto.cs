namespace ScholarTrend.Application.DTOs.Notifications;

public class NotificationSettingDto
{
    public bool EmailEnabled { get; set; }
    public bool TopicAlertEnabled { get; set; }
    public string Frequency { get; set; } = "Daily";
}
