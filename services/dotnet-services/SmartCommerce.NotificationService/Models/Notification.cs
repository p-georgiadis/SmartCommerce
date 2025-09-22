using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartCommerce.NotificationService.Models;

[Table("Notifications")]
public class Notification
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [StringLength(100)]
    public string Type { get; set; } = string.Empty; // order, system, promotion, security

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;

    [StringLength(50)]
    public string Priority { get; set; } = "normal"; // low, normal, high, critical

    [StringLength(50)]
    public string Status { get; set; } = "pending"; // pending, sent, delivered, failed, read

    [StringLength(100)]
    public string? Category { get; set; }

    [StringLength(500)]
    public string? ActionUrl { get; set; }

    [StringLength(100)]
    public string? ActionText { get; set; }

    public string? Metadata { get; set; } // JSON data

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReadAt { get; set; }

    public DateTime? ScheduledAt { get; set; }

    public DateTime? SentAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    // Delivery channels
    public bool SendPush { get; set; } = true;
    public bool SendEmail { get; set; } = false;
    public bool SendSms { get; set; } = false;
    public bool SendInApp { get; set; } = true;

    // Delivery tracking
    public DateTime? EmailSentAt { get; set; }
    public DateTime? SmsSentAt { get; set; }
    public DateTime? PushSentAt { get; set; }

    public string? EmailStatus { get; set; }
    public string? SmsStatus { get; set; }
    public string? PushStatus { get; set; }

    public string? FailureReason { get; set; }
    public int RetryCount { get; set; } = 0;
    public DateTime? NextRetryAt { get; set; }

    // Navigation properties
    public virtual ICollection<NotificationDelivery> Deliveries { get; set; } = new List<NotificationDelivery>();
}

[Table("NotificationTemplates")]
public class NotificationTemplate
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Type { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(200)]
    public string? TitleTemplate { get; set; }

    [Required]
    public string MessageTemplate { get; set; } = string.Empty;

    public string? EmailTemplate { get; set; }

    public string? SmsTemplate { get; set; }

    public string? PushTemplate { get; set; }

    [StringLength(50)]
    public string DefaultPriority { get; set; } = "normal";

    public bool IsActive { get; set; } = true;

    public string? DefaultMetadata { get; set; } // JSON

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Guid CreatedBy { get; set; }
}

[Table("NotificationDeliveries")]
public class NotificationDelivery
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid NotificationId { get; set; }

    [Required]
    [StringLength(50)]
    public string Channel { get; set; } = string.Empty; // email, sms, push, signalr

    [StringLength(500)]
    public string? Recipient { get; set; }

    [StringLength(50)]
    public string Status { get; set; } = "pending"; // pending, sent, delivered, failed

    public string? Content { get; set; } // The actual message sent

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SentAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public string? FailureReason { get; set; }

    public string? ExternalId { get; set; } // Provider-specific ID

    public string? Response { get; set; } // Provider response

    // Navigation properties
    [ForeignKey("NotificationId")]
    public virtual Notification Notification { get; set; } = null!;
}

[Table("UserNotificationPreferences")]
public class UserNotificationPreference
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [StringLength(100)]
    public string NotificationType { get; set; } = string.Empty;

    public bool EnablePush { get; set; } = true;
    public bool EnableEmail { get; set; } = true;
    public bool EnableSms { get; set; } = false;
    public bool EnableInApp { get; set; } = true;

    [StringLength(100)]
    public string? QuietHoursStart { get; set; } // HH:mm format
    [StringLength(100)]
    public string? QuietHoursEnd { get; set; } // HH:mm format

    [StringLength(50)]
    public string? Timezone { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("NotificationSubscriptions")]
public class NotificationSubscription
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [StringLength(50)]
    public string Platform { get; set; } = string.Empty; // web, ios, android

    [Required]
    [StringLength(500)]
    public string Endpoint { get; set; } = string.Empty;

    [StringLength(500)]
    public string? P256dh { get; set; }

    [StringLength(500)]
    public string? Auth { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }

    [StringLength(200)]
    public string? UserAgent { get; set; }

    [StringLength(50)]
    public string? DeviceId { get; set; }
}