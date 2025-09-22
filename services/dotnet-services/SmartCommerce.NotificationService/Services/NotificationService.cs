using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using SmartCommerce.NotificationService.Data;
using SmartCommerce.NotificationService.DTOs;
using SmartCommerce.NotificationService.Models;
using System.Text.Json;

namespace SmartCommerce.NotificationService.Services;

public class NotificationService : INotificationService
{
    private readonly NotificationDbContext _context;
    private readonly IMapper _mapper;
    private readonly IDistributedCache _cache;
    private readonly INotificationDeliveryService _deliveryService;
    private readonly IRealTimeNotificationService _realTimeService;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        NotificationDbContext context,
        IMapper mapper,
        IDistributedCache cache,
        INotificationDeliveryService deliveryService,
        IRealTimeNotificationService realTimeService,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _mapper = mapper;
        _cache = cache;
        _deliveryService = deliveryService;
        _realTimeService = realTimeService;
        _logger = logger;
    }

    public async Task<NotificationDto> CreateNotificationAsync(NotificationCreateDto createDto)
    {
        var notification = _mapper.Map<Notification>(createDto);
        notification.Metadata = createDto.Metadata != null ? JsonSerializer.Serialize(createDto.Metadata) : null;

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        var notificationDto = _mapper.Map<NotificationDto>(notification);

        // Send real-time notification
        var realTimeNotification = _mapper.Map<RealTimeNotificationDto>(notificationDto);
        await _realTimeService.SendToUserAsync(notification.UserId, realTimeNotification);

        // Queue for delivery if not scheduled
        if (notification.ScheduledAt == null || notification.ScheduledAt <= DateTime.UtcNow)
        {
            await _deliveryService.DeliverNotificationAsync(notification.Id);
        }

        // Update unread count cache
        await InvalidateUnreadCountCacheAsync(notification.UserId);

        _logger.LogInformation("Notification created: {NotificationId} for user {UserId}", notification.Id, notification.UserId);

        return notificationDto;
    }

    public async Task<List<NotificationDto>> CreateBulkNotificationAsync(BulkNotificationCreateDto createDto)
    {
        var notifications = new List<Notification>();

        foreach (var userId in createDto.UserIds)
        {
            var notification = new Notification
            {
                UserId = userId,
                Type = createDto.Type,
                Title = createDto.Title,
                Message = createDto.Message,
                Priority = createDto.Priority,
                Category = createDto.Category,
                ActionUrl = createDto.ActionUrl,
                ActionText = createDto.ActionText,
                Metadata = createDto.Metadata != null ? JsonSerializer.Serialize(createDto.Metadata) : null,
                ScheduledAt = createDto.ScheduledAt,
                ExpiresAt = createDto.ExpiresAt,
                SendPush = createDto.SendPush,
                SendEmail = createDto.SendEmail,
                SendSms = createDto.SendSms,
                SendInApp = createDto.SendInApp
            };

            notifications.Add(notification);
        }

        _context.Notifications.AddRange(notifications);
        await _context.SaveChangesAsync();

        var notificationDtos = _mapper.Map<List<NotificationDto>>(notifications);

        // Send real-time notifications
        var realTimeNotifications = notificationDtos.Select(n => _mapper.Map<RealTimeNotificationDto>(n)).ToList();
        await _realTimeService.SendToUsersAsync(createDto.UserIds, realTimeNotifications.First());

        // Queue for delivery if not scheduled
        var immediateNotifications = notifications.Where(n => n.ScheduledAt == null || n.ScheduledAt <= DateTime.UtcNow).ToList();
        if (immediateNotifications.Any())
        {
            await _deliveryService.DeliverNotificationAsync(immediateNotifications.First().Id); // Simplified - in reality, process all
        }

        // Update unread count cache for all users
        foreach (var userId in createDto.UserIds)
        {
            await InvalidateUnreadCountCacheAsync(userId);
        }

        _logger.LogInformation("Bulk notifications created: {Count} notifications for {UserCount} users",
            notifications.Count, createDto.UserIds.Count);

        return notificationDtos;
    }

    public async Task<NotificationDto> CreateFromTemplateAsync(TemplateNotificationCreateDto createDto)
    {
        var template = await _context.NotificationTemplates
            .FirstOrDefaultAsync(t => t.Name == createDto.TemplateName && t.IsActive);

        if (template == null)
        {
            throw new KeyNotFoundException($"Template '{createDto.TemplateName}' not found or inactive");
        }

        // Process template with parameters
        var title = await ProcessTemplateStringAsync(template.TitleTemplate ?? template.Name, createDto.Parameters);
        var message = await ProcessTemplateStringAsync(template.MessageTemplate, createDto.Parameters);

        var notification = new Notification
        {
            UserId = createDto.UserId,
            Type = template.Type,
            Title = title,
            Message = message,
            Priority = createDto.Priority ?? template.DefaultPriority,
            ScheduledAt = createDto.ScheduledAt,
            ExpiresAt = createDto.ExpiresAt,
            SendPush = createDto.SendPush ?? true,
            SendEmail = createDto.SendEmail ?? false,
            SendSms = createDto.SendSms ?? false,
            SendInApp = createDto.SendInApp ?? true,
            Metadata = JsonSerializer.Serialize(createDto.Parameters)
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        var notificationDto = _mapper.Map<NotificationDto>(notification);

        // Send real-time notification
        var realTimeNotification = _mapper.Map<RealTimeNotificationDto>(notificationDto);
        await _realTimeService.SendToUserAsync(notification.UserId, realTimeNotification);

        // Queue for delivery if not scheduled
        if (notification.ScheduledAt == null || notification.ScheduledAt <= DateTime.UtcNow)
        {
            await _deliveryService.DeliverNotificationAsync(notification.Id);
        }

        await InvalidateUnreadCountCacheAsync(notification.UserId);

        _logger.LogInformation("Notification created from template '{TemplateName}': {NotificationId} for user {UserId}",
            createDto.TemplateName, notification.Id, notification.UserId);

        return notificationDto;
    }

    public async Task<NotificationDto?> GetNotificationAsync(Guid id)
    {
        var notification = await _context.Notifications
            .Include(n => n.Deliveries)
            .FirstOrDefaultAsync(n => n.Id == id);

        return notification != null ? _mapper.Map<NotificationDto>(notification) : null;
    }

    public async Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId, int skip = 0, int take = 50, string? type = null, bool? isRead = null)
    {
        var query = _context.Notifications
            .Where(n => n.UserId == userId);

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(n => n.Type == type);
        }

        if (isRead.HasValue)
        {
            query = query.Where(n => n.IsRead == isRead.Value);
        }

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Include(n => n.Deliveries)
            .ToListAsync();

        return _mapper.Map<List<NotificationDto>>(notifications);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        var cacheKey = $"unread_count:{userId}";
        var cachedCount = await _cache.GetStringAsync(cacheKey);

        if (!string.IsNullOrEmpty(cachedCount) && int.TryParse(cachedCount, out var count))
        {
            return count;
        }

        var unreadCount = await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);

        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };

        await _cache.SetStringAsync(cacheKey, unreadCount.ToString(), cacheOptions);

        return unreadCount;
    }

    public async Task MarkNotificationAsReadAsync(Guid notificationId)
    {
        var notification = await _context.Notifications.FindAsync(notificationId);
        if (notification != null && !notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            notification.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await InvalidateUnreadCountCacheAsync(notification.UserId);

            // Notify real-time about unread count change
            var newCount = await GetUnreadCountAsync(notification.UserId);
            await _realTimeService.NotifyUnreadCountChangeAsync(notification.UserId, newCount);

            _logger.LogInformation("Notification marked as read: {NotificationId}", notificationId);
        }
    }

    public async Task MarkNotificationsAsReadAsync(Guid userId, List<Guid> notificationIds)
    {
        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId && notificationIds.Contains(n.Id) && !n.IsRead)
            .ToListAsync();

        if (notifications.Any())
        {
            var now = DateTime.UtcNow;
            foreach (var notification in notifications)
            {
                notification.IsRead = true;
                notification.ReadAt = now;
                notification.UpdatedAt = now;
            }

            await _context.SaveChangesAsync();
            await InvalidateUnreadCountCacheAsync(userId);

            // Notify real-time about unread count change
            var newCount = await GetUnreadCountAsync(userId);
            await _realTimeService.NotifyUnreadCountChangeAsync(userId, newCount);

            _logger.LogInformation("Marked {Count} notifications as read for user {UserId}",
                notifications.Count, userId);
        }
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        var unreadNotifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        if (unreadNotifications.Any())
        {
            var now = DateTime.UtcNow;
            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
                notification.ReadAt = now;
                notification.UpdatedAt = now;
            }

            await _context.SaveChangesAsync();
            await InvalidateUnreadCountCacheAsync(userId);

            // Notify real-time about unread count change
            await _realTimeService.NotifyUnreadCountChangeAsync(userId, 0);

            _logger.LogInformation("Marked all {Count} notifications as read for user {UserId}",
                unreadNotifications.Count, userId);
        }
    }

    public async Task DeleteNotificationAsync(Guid id)
    {
        var notification = await _context.Notifications.FindAsync(id);
        if (notification != null)
        {
            var userId = notification.UserId;
            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            await InvalidateUnreadCountCacheAsync(userId);

            _logger.LogInformation("Notification deleted: {NotificationId}", id);
        }
    }

    public async Task DeleteUserNotificationsAsync(Guid userId, List<Guid> notificationIds)
    {
        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId && notificationIds.Contains(n.Id))
            .ToListAsync();

        if (notifications.Any())
        {
            _context.Notifications.RemoveRange(notifications);
            await _context.SaveChangesAsync();

            await InvalidateUnreadCountCacheAsync(userId);

            _logger.LogInformation("Deleted {Count} notifications for user {UserId}",
                notifications.Count, userId);
        }
    }

    public async Task SendNotificationAsync(Guid notificationId)
    {
        await _deliveryService.DeliverNotificationAsync(notificationId);
    }

    public async Task SendBulkNotificationsAsync(List<Guid> notificationIds)
    {
        foreach (var notificationId in notificationIds)
        {
            await _deliveryService.DeliverNotificationAsync(notificationId);
        }
    }

    public async Task ProcessScheduledNotificationsAsync()
    {
        var scheduledNotifications = await _context.Notifications
            .Where(n => n.ScheduledAt.HasValue &&
                       n.ScheduledAt <= DateTime.UtcNow &&
                       n.Status == "pending")
            .Take(100) // Process in batches
            .ToListAsync();

        foreach (var notification in scheduledNotifications)
        {
            try
            {
                await _deliveryService.DeliverNotificationAsync(notification.Id);
                _logger.LogInformation("Processed scheduled notification: {NotificationId}", notification.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process scheduled notification: {NotificationId}", notification.Id);
            }
        }
    }

    public async Task RetryFailedNotificationsAsync()
    {
        var failedNotifications = await _context.Notifications
            .Where(n => n.Status == "failed" &&
                       n.RetryCount < 3 &&
                       (n.NextRetryAt == null || n.NextRetryAt <= DateTime.UtcNow))
            .Take(50) // Process in batches
            .ToListAsync();

        foreach (var notification in failedNotifications)
        {
            try
            {
                await _deliveryService.DeliverNotificationAsync(notification.Id);
                _logger.LogInformation("Retried failed notification: {NotificationId}", notification.Id);
            }
            catch (Exception ex)
            {
                notification.RetryCount++;
                notification.NextRetryAt = DateTime.UtcNow.AddMinutes(Math.Pow(2, notification.RetryCount)); // Exponential backoff
                notification.FailureReason = ex.Message;
                await _context.SaveChangesAsync();

                _logger.LogError(ex, "Failed to retry notification: {NotificationId}. Retry count: {RetryCount}",
                    notification.Id, notification.RetryCount);
            }
        }
    }

    // Template methods
    public async Task<NotificationTemplateDto?> GetTemplateAsync(Guid id)
    {
        var template = await _context.NotificationTemplates.FindAsync(id);
        return template != null ? _mapper.Map<NotificationTemplateDto>(template) : null;
    }

    public async Task<NotificationTemplateDto?> GetTemplateByNameAsync(string name)
    {
        var template = await _context.NotificationTemplates
            .FirstOrDefaultAsync(t => t.Name == name);
        return template != null ? _mapper.Map<NotificationTemplateDto>(template) : null;
    }

    public async Task<List<NotificationTemplateDto>> GetTemplatesAsync()
    {
        var templates = await _context.NotificationTemplates
            .OrderBy(t => t.Type)
            .ThenBy(t => t.Name)
            .ToListAsync();

        return _mapper.Map<List<NotificationTemplateDto>>(templates);
    }

    public async Task<NotificationTemplateDto> CreateTemplateAsync(NotificationTemplateCreateDto createDto)
    {
        var template = _mapper.Map<NotificationTemplate>(createDto);
        template.DefaultMetadata = createDto.DefaultMetadata != null ? JsonSerializer.Serialize(createDto.DefaultMetadata) : null;
        template.CreatedBy = Guid.Empty; // Should be set from current user context

        _context.NotificationTemplates.Add(template);
        await _context.SaveChangesAsync();

        return _mapper.Map<NotificationTemplateDto>(template);
    }

    public async Task<NotificationTemplateDto> UpdateTemplateAsync(Guid id, NotificationTemplateCreateDto updateDto)
    {
        var template = await _context.NotificationTemplates.FindAsync(id);
        if (template == null)
        {
            throw new KeyNotFoundException($"Template with ID {id} not found");
        }

        _mapper.Map(updateDto, template);
        template.DefaultMetadata = updateDto.DefaultMetadata != null ? JsonSerializer.Serialize(updateDto.DefaultMetadata) : null;
        template.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return _mapper.Map<NotificationTemplateDto>(template);
    }

    public async Task DeleteTemplateAsync(Guid id)
    {
        var template = await _context.NotificationTemplates.FindAsync(id);
        if (template != null)
        {
            _context.NotificationTemplates.Remove(template);
            await _context.SaveChangesAsync();
        }
    }

    // User preferences methods - simplified implementations
    public async Task<List<UserNotificationPreferenceDto>> GetUserPreferencesAsync(Guid userId)
    {
        var preferences = await _context.UserNotificationPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync();

        return _mapper.Map<List<UserNotificationPreferenceDto>>(preferences);
    }

    public async Task<UserNotificationPreferenceDto?> GetUserPreferenceAsync(Guid userId, string notificationType)
    {
        var preference = await _context.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.NotificationType == notificationType);

        return preference != null ? _mapper.Map<UserNotificationPreferenceDto>(preference) : null;
    }

    public async Task<UserNotificationPreferenceDto> UpdateUserPreferencesAsync(Guid userId, string notificationType, UserNotificationPreferenceUpdateDto updateDto)
    {
        var preference = await _context.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.NotificationType == notificationType);

        if (preference == null)
        {
            preference = new UserNotificationPreference
            {
                UserId = userId,
                NotificationType = notificationType
            };
            _context.UserNotificationPreferences.Add(preference);
        }

        _mapper.Map(updateDto, preference);
        preference.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return _mapper.Map<UserNotificationPreferenceDto>(preference);
    }

    public async Task ResetUserPreferencesToDefaultAsync(Guid userId)
    {
        var preferences = await _context.UserNotificationPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync();

        _context.UserNotificationPreferences.RemoveRange(preferences);
        await _context.SaveChangesAsync();
    }

    // Subscription methods - simplified implementations
    public async Task<List<NotificationSubscriptionDto>> GetUserSubscriptionsAsync(Guid userId)
    {
        var subscriptions = await _context.NotificationSubscriptions
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync();

        return _mapper.Map<List<NotificationSubscriptionDto>>(subscriptions);
    }

    public async Task<NotificationSubscriptionDto> CreateSubscriptionAsync(Guid userId, NotificationSubscriptionCreateDto createDto)
    {
        // Deactivate existing subscriptions for the same platform/device
        var existingSubscriptions = await _context.NotificationSubscriptions
            .Where(s => s.UserId == userId &&
                       s.Platform == createDto.Platform &&
                       (s.DeviceId == createDto.DeviceId || s.Endpoint == createDto.Endpoint))
            .ToListAsync();

        foreach (var existing in existingSubscriptions)
        {
            existing.IsActive = false;
        }

        var subscription = _mapper.Map<NotificationSubscription>(createDto);
        subscription.UserId = userId;

        _context.NotificationSubscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        return _mapper.Map<NotificationSubscriptionDto>(subscription);
    }

    public async Task UpdateSubscriptionAsync(Guid subscriptionId, bool isActive)
    {
        var subscription = await _context.NotificationSubscriptions.FindAsync(subscriptionId);
        if (subscription != null)
        {
            subscription.IsActive = isActive;
            subscription.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteSubscriptionAsync(Guid subscriptionId)
    {
        var subscription = await _context.NotificationSubscriptions.FindAsync(subscriptionId);
        if (subscription != null)
        {
            _context.NotificationSubscriptions.Remove(subscription);
            await _context.SaveChangesAsync();
        }
    }

    public async Task CleanupInactiveSubscriptionsAsync()
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var inactiveSubscriptions = await _context.NotificationSubscriptions
            .Where(s => !s.IsActive || s.LastUsedAt < cutoff)
            .ToListAsync();

        _context.NotificationSubscriptions.RemoveRange(inactiveSubscriptions);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Cleaned up {Count} inactive subscriptions", inactiveSubscriptions.Count);
    }

    // Statistics methods - simplified implementations
    public async Task<NotificationStatsDto> GetNotificationStatsAsync(Guid? userId = null)
    {
        var query = _context.Notifications.AsQueryable();
        if (userId.HasValue)
        {
            query = query.Where(n => n.UserId == userId.Value);
        }

        var total = await query.CountAsync();
        var unread = await query.CountAsync(n => !n.IsRead);
        var today = await query.CountAsync(n => n.CreatedAt.Date == DateTime.UtcNow.Date);
        var thisWeek = await query.CountAsync(n => n.CreatedAt >= DateTime.UtcNow.AddDays(-7));
        var thisMonth = await query.CountAsync(n => n.CreatedAt >= DateTime.UtcNow.AddDays(-30));

        return new NotificationStatsDto
        {
            TotalNotifications = total,
            UnreadNotifications = unread,
            SentToday = today,
            SentThisWeek = thisWeek,
            SentThisMonth = thisMonth
        };
    }

    public async Task<NotificationStatsDto> GetNotificationStatsByDateRangeAsync(DateTime startDate, DateTime endDate, Guid? userId = null)
    {
        var query = _context.Notifications.Where(n => n.CreatedAt >= startDate && n.CreatedAt <= endDate);
        if (userId.HasValue)
        {
            query = query.Where(n => n.UserId == userId.Value);
        }

        var total = await query.CountAsync();
        var unread = await query.CountAsync(n => !n.IsRead);

        return new NotificationStatsDto
        {
            TotalNotifications = total,
            UnreadNotifications = unread
        };
    }

    private async Task InvalidateUnreadCountCacheAsync(Guid userId)
    {
        var cacheKey = $"unread_count:{userId}";
        await _cache.RemoveAsync(cacheKey);
    }

    private async Task<string> ProcessTemplateStringAsync(string template, Dictionary<string, object> parameters)
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