namespace ScholarTrend.Domain.Entities;
public class NotificationSetting
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public bool EmailEnabled { get; set; } = true;
    public bool TopicAlertEnabled { get; set; } = true;
    public string Frequency { get; set; } = "Daily";
    public User User { get; set; } = null!;
}