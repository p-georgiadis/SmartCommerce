using System.ComponentModel.DataAnnotations;

namespace SmartCommerce.NotificationService.DTOs;

public record NotificationCreateDto
{
    [Required]
    public Guid UserId { get; init; }

    [Required]
    [StringLength(100)]
    public string Type { get; init; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Message { get; init; } = string.Empty;

    public string Priority { get; init; } = "normal";

    public string? Category { get; init; }

    public string? ActionUrl { get; init; }

    public string? ActionText { get; init; }

    public Dictionary<string, object>? Metadata { get; init; }

    public DateTime? ScheduledAt { get; init; }

    public DateTime? ExpiresAt { get; init; }

    public bool SendPush { get; init; } = true;
    public bool SendEmail { get; init; } = false;
    public bool SendSms { get; init; } = false;
    public bool SendInApp { get; init; } = true;
}

public record BulkNotificationCreateDto
{
    [Required]
    public List<Guid> UserIds { get; init; } = new();

    [Required]
    [StringLength(100)]
    public string Type { get; init; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Message { get; init; } = string.Empty;

    public string Priority { get; init; } = "normal";

    public string? Category { get; init; }

    public string? ActionUrl { get; init; }

    public string? ActionText { get; init; }

    public Dictionary<string, object>? Metadata { get; init; }

    public DateTime? ScheduledAt { get; init; }

    public DateTime? ExpiresAt { get; init; }

    public bool SendPush { get; init; } = true;
    public bool SendEmail { get; init; } = false;
    public bool SendSms { get; init; } = false;
    public bool SendInApp { get; init; } = true;
}

public record TemplateNotificationCreateDto
{
    [Required]
    public Guid UserId { get; init; }

    [Required]
    public string TemplateName { get; init; } = string.Empty;

    public Dictionary<string, object> Parameters { get; init; } = new();

    public string? Priority { get; init; }

    public DateTime? ScheduledAt { get; init; }

    public DateTime? ExpiresAt { get; init; }

    public bool? SendPush { get; init; }
    public bool? SendEmail { get; init; }
    public bool? SendSms { get; init; }
    public bool? SendInApp { get; init; }
}

public record NotificationDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? Category { get; init; }
    public string? ActionUrl { get; init; }
    public string? ActionText { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
    public bool IsRead { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? ReadAt { get; init; }
    public DateTime? ScheduledAt { get; init; }
    public DateTime? SentAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public bool SendPush { get; init; }
    public bool SendEmail { get; init; }
    public bool SendSms { get; init; }
    public bool SendInApp { get; init; }
    public List<NotificationDeliveryDto> Deliveries { get; init; } = new();
}

public record NotificationDeliveryDto
{
    public Guid Id { get; init; }
    public string Channel { get; init; } = string.Empty;
    public string? Recipient { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? SentAt { get; init; }
    public DateTime? DeliveredAt { get; init; }
    public string? FailureReason { get; init; }
    public string? ExternalId { get; init; }
}

public record NotificationTemplateDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? TitleTemplate { get; init; }
    public string MessageTemplate { get; init; } = string.Empty;
    public string? EmailTemplate { get; init; }
    public string? SmsTemplate { get; init; }
    public string? PushTemplate { get; init; }
    public string DefaultPriority { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public Dictionary<string, object>? DefaultMetadata { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public Guid CreatedBy { get; init; }
}

public record NotificationTemplateCreateDto
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public string Type { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? TitleTemplate { get; init; }

    [Required]
    public string MessageTemplate { get; init; } = string.Empty;

    public string? EmailTemplate { get; init; }

    public string? SmsTemplate { get; init; }

    public string? PushTemplate { get; init; }

    public string DefaultPriority { get; init; } = "normal";

    public Dictionary<string, object>? DefaultMetadata { get; init; }
}

public record UserNotificationPreferenceDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string NotificationType { get; init; } = string.Empty;
    public bool EnablePush { get; init; }
    public bool EnableEmail { get; init; }
    public bool EnableSms { get; init; }
    public bool EnableInApp { get; init; }
    public string? QuietHoursStart { get; init; }
    public string? QuietHoursEnd { get; init; }
    public string? Timezone { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record UserNotificationPreferenceUpdateDto
{
    public bool? EnablePush { get; init; }
    public bool? EnableEmail { get; init; }
    public bool? EnableSms { get; init; }
    public bool? EnableInApp { get; init; }
    public string? QuietHoursStart { get; init; }
    public string? QuietHoursEnd { get; init; }
    public string? Timezone { get; init; }
}

public record NotificationSubscriptionDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Platform { get; init; } = string.Empty;
    public string Endpoint { get; init; } = string.Empty;
    public string? P256dh { get; init; }
    public string? Auth { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? LastUsedAt { get; init; }
    public string? UserAgent { get; init; }
    public string? DeviceId { get; init; }
}

public record NotificationSubscriptionCreateDto
{
    [Required]
    public string Platform { get; init; } = string.Empty;

    [Required]
    public string Endpoint { get; init; } = string.Empty;

    public string? P256dh { get; init; }

    public string? Auth { get; init; }

    public string? UserAgent { get; init; }

    public string? DeviceId { get; init; }
}

public record NotificationStatsDto
{
    public int TotalNotifications { get; init; }
    public int UnreadNotifications { get; init; }
    public int SentToday { get; init; }
    public int SentThisWeek { get; init; }
    public int SentThisMonth { get; init; }
    public Dictionary<string, int> NotificationsByType { get; init; } = new();
    public Dictionary<string, int> NotificationsByStatus { get; init; } = new();
    public Dictionary<string, int> DeliveryByChannel { get; init; } = new();
    public double AverageDeliveryTime { get; init; }
    public double DeliverySuccessRate { get; init; }
}

public record RealTimeNotificationDto
{
    public Guid Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string? Category { get; init; }
    public string? ActionUrl { get; init; }
    public string? ActionText { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record MarkAsReadDto
{
    [Required]
    public List<Guid> NotificationIds { get; init; } = new();
}