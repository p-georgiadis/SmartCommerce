using SmartCommerce.NotificationService.DTOs;

namespace SmartCommerce.NotificationService.Services;

public interface INotificationService
{
    // Core notification operations
    Task<NotificationDto> CreateNotificationAsync(NotificationCreateDto createDto);
    Task<List<NotificationDto>> CreateBulkNotificationAsync(BulkNotificationCreateDto createDto);
    Task<NotificationDto> CreateFromTemplateAsync(TemplateNotificationCreateDto createDto);
    Task<NotificationDto?> GetNotificationAsync(Guid id);
    Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId, int skip = 0, int take = 50, string? type = null, bool? isRead = null);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task MarkNotificationAsReadAsync(Guid notificationId);
    Task MarkNotificationsAsReadAsync(Guid userId, List<Guid> notificationIds);
    Task MarkAllAsReadAsync(Guid userId);
    Task DeleteNotificationAsync(Guid id);
    Task DeleteUserNotificationsAsync(Guid userId, List<Guid> notificationIds);

    // Delivery and sending
    Task SendNotificationAsync(Guid notificationId);
    Task SendBulkNotificationsAsync(List<Guid> notificationIds);
    Task ProcessScheduledNotificationsAsync();
    Task RetryFailedNotificationsAsync();

    // Template management
    Task<NotificationTemplateDto?> GetTemplateAsync(Guid id);
    Task<NotificationTemplateDto?> GetTemplateByNameAsync(string name);
    Task<List<NotificationTemplateDto>> GetTemplatesAsync();
    Task<NotificationTemplateDto> CreateTemplateAsync(NotificationTemplateCreateDto createDto);
    Task<NotificationTemplateDto> UpdateTemplateAsync(Guid id, NotificationTemplateCreateDto updateDto);
    Task DeleteTemplateAsync(Guid id);

    // User preferences
    Task<List<UserNotificationPreferenceDto>> GetUserPreferencesAsync(Guid userId);
    Task<UserNotificationPreferenceDto?> GetUserPreferenceAsync(Guid userId, string notificationType);
    Task<UserNotificationPreferenceDto> UpdateUserPreferencesAsync(Guid userId, string notificationType, UserNotificationPreferenceUpdateDto updateDto);
    Task ResetUserPreferencesToDefaultAsync(Guid userId);

    // Subscriptions
    Task<List<NotificationSubscriptionDto>> GetUserSubscriptionsAsync(Guid userId);
    Task<NotificationSubscriptionDto> CreateSubscriptionAsync(Guid userId, NotificationSubscriptionCreateDto createDto);
    Task UpdateSubscriptionAsync(Guid subscriptionId, bool isActive);
    Task DeleteSubscriptionAsync(Guid subscriptionId);
    Task CleanupInactiveSubscriptionsAsync();

    // Statistics and reporting
    Task<NotificationStatsDto> GetNotificationStatsAsync(Guid? userId = null);
    Task<NotificationStatsDto> GetNotificationStatsByDateRangeAsync(DateTime startDate, DateTime endDate, Guid? userId = null);
}

public interface INotificationDeliveryService
{
    Task DeliverNotificationAsync(Guid notificationId);
    Task DeliverNotificationViaChannelAsync(Guid notificationId, string channel);
    Task<bool> CanDeliverToChannelAsync(Guid userId, string channel, string notificationType);
    Task<string> ProcessTemplateAsync(string template, Dictionary<string, object> parameters);
}

public interface IEmailNotificationService
{
    Task<bool> SendEmailAsync(string to, string subject, string htmlContent, string? textContent = null);
    Task<bool> SendBulkEmailAsync(List<string> recipients, string subject, string htmlContent, string? textContent = null);
    Task<string> GetDeliveryStatusAsync(string messageId);
}

public interface ISmsNotificationService
{
    Task<bool> SendSmsAsync(string to, string message);
    Task<bool> SendBulkSmsAsync(List<string> recipients, string message);
    Task<string> GetDeliveryStatusAsync(string messageId);
}

public interface IPushNotificationService
{
    Task<bool> SendPushNotificationAsync(Guid userId, string title, string message, Dictionary<string, object>? data = null);
    Task<bool> SendPushNotificationToSubscriptionAsync(NotificationSubscriptionDto subscription, string title, string message, Dictionary<string, object>? data = null);
    Task<bool> SendBulkPushNotificationAsync(List<Guid> userIds, string title, string message, Dictionary<string, object>? data = null);
    Task<bool> ValidateSubscriptionAsync(NotificationSubscriptionDto subscription);
}

public interface IRealTimeNotificationService
{
    Task SendToUserAsync(Guid userId, RealTimeNotificationDto notification);
    Task SendToUsersAsync(List<Guid> userIds, RealTimeNotificationDto notification);
    Task SendToGroupAsync(string groupName, RealTimeNotificationDto notification);
    Task SendToRoleAsync(string roleName, RealTimeNotificationDto notification);
    Task SendBroadcastAsync(RealTimeNotificationDto notification);
    Task NotifyUnreadCountChangeAsync(Guid userId, int newCount);
}

public interface INotificationTemplateEngine
{
    Task<string> ProcessTemplateAsync(string template, Dictionary<string, object> parameters);
    Task<NotificationDto> CreateNotificationFromTemplateAsync(string templateName, Guid userId, Dictionary<string, object> parameters);
    Task<bool> ValidateTemplateAsync(string template);
    Task<List<string>> ExtractTemplateParametersAsync(string template);
}