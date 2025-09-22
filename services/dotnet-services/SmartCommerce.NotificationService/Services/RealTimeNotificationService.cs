using Microsoft.AspNetCore.SignalR;
using SmartCommerce.NotificationService.DTOs;
using SmartCommerce.NotificationService.Hubs;

namespace SmartCommerce.NotificationService.Services;

public class RealTimeNotificationService : IRealTimeNotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<RealTimeNotificationService> _logger;

    public RealTimeNotificationService(
        IHubContext<NotificationHub> hubContext,
        ILogger<RealTimeNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendToUserAsync(Guid userId, RealTimeNotificationDto notification)
    {
        try
        {
            await _hubContext.Clients.Group($"user_{userId}")
                .SendAsync("ReceiveNotification", notification);

            _logger.LogInformation("Real-time notification sent to user {UserId}: {NotificationId}",
                userId, notification.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send real-time notification to user {UserId}", userId);
        }
    }

    public async Task SendToUsersAsync(List<Guid> userIds, RealTimeNotificationDto notification)
    {
        try
        {
            var groups = userIds.Select(id => $"user_{id}").ToList();
            await _hubContext.Clients.Groups(groups)
                .SendAsync("ReceiveNotification", notification);

            _logger.LogInformation("Real-time notification sent to {UserCount} users: {NotificationId}",
                userIds.Count, notification.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send real-time notification to multiple users");
        }
    }

    public async Task SendToGroupAsync(string groupName, RealTimeNotificationDto notification)
    {
        try
        {
            await _hubContext.Clients.Group(groupName)
                .SendAsync("ReceiveNotification", notification);

            _logger.LogInformation("Real-time notification sent to group {GroupName}: {NotificationId}",
                groupName, notification.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send real-time notification to group {GroupName}", groupName);
        }
    }

    public async Task SendToRoleAsync(string roleName, RealTimeNotificationDto notification)
    {
        try
        {
            await _hubContext.Clients.Group($"role_{roleName}")
                .SendAsync("ReceiveNotification", notification);

            _logger.LogInformation("Real-time notification sent to role {RoleName}: {NotificationId}",
                roleName, notification.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send real-time notification to role {RoleName}", roleName);
        }
    }

    public async Task SendBroadcastAsync(RealTimeNotificationDto notification)
    {
        try
        {
            await _hubContext.Clients.All
                .SendAsync("ReceiveNotification", notification);

            _logger.LogInformation("Real-time notification broadcasted to all users: {NotificationId}",
                notification.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast real-time notification");
        }
    }

    public async Task NotifyUnreadCountChangeAsync(Guid userId, int newCount)
    {
        try
        {
            await _hubContext.Clients.Group($"user_{userId}")
                .SendAsync("UnreadCountChanged", newCount);

            _logger.LogDebug("Unread count notification sent to user {UserId}: {Count}",
                userId, newCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send unread count notification to user {UserId}", userId);
        }
    }
}

public class NotificationDeliveryService : INotificationDeliveryService
{
    private readonly NotificationDbContext _context;
    private readonly IEmailNotificationService _emailService;
    private readonly ISmsNotificationService _smsService;
    private readonly IPushNotificationService _pushService;
    private readonly IRealTimeNotificationService _realTimeService;
    private readonly ILogger<NotificationDeliveryService> _logger;

    public NotificationDeliveryService(
        NotificationDbContext context,
        IEmailNotificationService emailService,
        ISmsNotificationService smsService,
        IPushNotificationService pushService,
        IRealTimeNotificationService realTimeService,
        ILogger<NotificationDeliveryService> logger)
    {
        _context = context;
        _emailService = emailService;
        _smsService = smsService;
        _pushService = pushService;
        _realTimeService = realTimeService;
        _logger = logger;
    }

    public async Task DeliverNotificationAsync(Guid notificationId)
    {
        var notification = await _context.Notifications
            .Include(n => n.Deliveries)
            .FirstOrDefaultAsync(n => n.Id == notificationId);

        if (notification == null)
        {
            _logger.LogWarning("Notification not found for delivery: {NotificationId}", notificationId);
            return;
        }

        try
        {
            notification.Status = "sending";
            notification.SentAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var deliveryTasks = new List<Task>();

            // Email delivery
            if (notification.SendEmail && await CanDeliverToChannelAsync(notification.UserId, "email", notification.Type))
            {
                deliveryTasks.Add(DeliverNotificationViaChannelAsync(notificationId, "email"));
            }

            // SMS delivery
            if (notification.SendSms && await CanDeliverToChannelAsync(notification.UserId, "sms", notification.Type))
            {
                deliveryTasks.Add(DeliverNotificationViaChannelAsync(notificationId, "sms"));
            }

            // Push notification delivery
            if (notification.SendPush && await CanDeliverToChannelAsync(notification.UserId, "push", notification.Type))
            {
                deliveryTasks.Add(DeliverNotificationViaChannelAsync(notificationId, "push"));
            }

            // Execute all deliveries in parallel
            await Task.WhenAll(deliveryTasks);

            notification.Status = "sent";
            await _context.SaveChangesAsync();

            _logger.LogInformation("Notification delivered successfully: {NotificationId}", notificationId);
        }
        catch (Exception ex)
        {
            notification.Status = "failed";
            notification.FailureReason = ex.Message;
            notification.RetryCount++;
            notification.NextRetryAt = DateTime.UtcNow.AddMinutes(Math.Pow(2, notification.RetryCount));

            await _context.SaveChangesAsync();

            _logger.LogError(ex, "Failed to deliver notification: {NotificationId}", notificationId);
        }
    }

    public async Task DeliverNotificationViaChannelAsync(Guid notificationId, string channel)
    {
        var notification = await _context.Notifications.FindAsync(notificationId);
        if (notification == null) return;

        var delivery = new NotificationDelivery
        {
            NotificationId = notificationId,
            Channel = channel,
            Status = "sending"
        };

        _context.NotificationDeliveries.Add(delivery);
        await _context.SaveChangesAsync();

        try
        {
            bool success = false;
            string? externalId = null;

            switch (channel.ToLower())
            {
                case "email":
                    success = await _emailService.SendEmailAsync(
                        "user@example.com", // In reality, get from user profile
                        notification.Title,
                        notification.Message);
                    break;

                case "sms":
                    success = await _smsService.SendSmsAsync(
                        "+1234567890", // In reality, get from user profile
                        notification.Message);
                    break;

                case "push":
                    success = await _pushService.SendPushNotificationAsync(
                        notification.UserId,
                        notification.Title,
                        notification.Message);
                    break;
            }

            delivery.Status = success ? "sent" : "failed";
            delivery.SentAt = DateTime.UtcNow;
            delivery.ExternalId = externalId;

            if (success)
            {
                // Update notification status for this channel
                switch (channel.ToLower())
                {
                    case "email":
                        notification.EmailSentAt = DateTime.UtcNow;
                        notification.EmailStatus = "sent";
                        break;
                    case "sms":
                        notification.SmsSentAt = DateTime.UtcNow;
                        notification.SmsStatus = "sent";
                        break;
                    case "push":
                        notification.PushSentAt = DateTime.UtcNow;
                        notification.PushStatus = "sent";
                        break;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Notification delivered via {Channel}: {NotificationId}, Success: {Success}",
                channel, notificationId, success);
        }
        catch (Exception ex)
        {
            delivery.Status = "failed";
            delivery.FailureReason = ex.Message;
            await _context.SaveChangesAsync();

            _logger.LogError(ex, "Failed to deliver notification via {Channel}: {NotificationId}",
                channel, notificationId);
        }
    }

    public async Task<bool> CanDeliverToChannelAsync(Guid userId, string channel, string notificationType)
    {
        // Check user preferences
        var preference = await _context.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.NotificationType == notificationType);

        if (preference == null)
        {
            // Default preferences - allow all except SMS
            return channel.ToLower() != "sms";
        }

        return channel.ToLower() switch
        {
            "email" => preference.EnableEmail,
            "sms" => preference.EnableSms,
            "push" => preference.EnablePush,
            "signalr" => preference.EnableInApp,
            _ => false
        };
    }

    public async Task<string> ProcessTemplateAsync(string template, Dictionary<string, object> parameters)
    {
        var result = template;

        foreach (var param in parameters)
        {
            var placeholder = $"{{{param.Key}}}";
            result = result.Replace(placeholder, param.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}