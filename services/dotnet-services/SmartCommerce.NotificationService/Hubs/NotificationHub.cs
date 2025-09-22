using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SmartCommerce.NotificationService.Services;
using System.Security.Claims;

namespace SmartCommerce.NotificationService.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(
        INotificationService notificationService,
        ILogger<NotificationHub> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();
        if (userId.HasValue)
        {
            // Add user to their personal group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

            // Add user to role-based groups
            var roles = GetUserRoles();
            foreach (var role in roles)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"role_{role}");
            }

            _logger.LogInformation("User {UserId} connected to notification hub with connection {ConnectionId}",
                userId, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetCurrentUserId();
        if (userId.HasValue)
        {
            _logger.LogInformation("User {UserId} disconnected from notification hub with connection {ConnectionId}",
                userId, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Join a specific notification group (e.g., order updates for a specific order)
    /// </summary>
    public async Task JoinGroup(string groupName)
    {
        var userId = GetCurrentUserId();
        if (userId.HasValue)
        {
            // Validate that user can join this group
            if (await CanJoinGroup(userId.Value, groupName))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
                _logger.LogInformation("User {UserId} joined group {GroupName}", userId, groupName);
            }
            else
            {
                _logger.LogWarning("User {UserId} attempted to join unauthorized group {GroupName}", userId, groupName);
            }
        }
    }

    /// <summary>
    /// Leave a specific notification group
    /// </summary>
    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        var userId = GetCurrentUserId();
        _logger.LogInformation("User {UserId} left group {GroupName}", userId, groupName);
    }

    /// <summary>
    /// Mark notifications as read
    /// </summary>
    public async Task MarkNotificationsAsRead(List<Guid> notificationIds)
    {
        var userId = GetCurrentUserId();
        if (userId.HasValue)
        {
            try
            {
                await _notificationService.MarkNotificationsAsReadAsync(userId.Value, notificationIds);

                // Notify the user that notifications were marked as read
                await Clients.Caller.SendAsync("NotificationsMarkedAsRead", notificationIds);

                _logger.LogInformation("User {UserId} marked {Count} notifications as read",
                    userId, notificationIds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notifications as read for user {UserId}", userId);
                await Clients.Caller.SendAsync("Error", "Failed to mark notifications as read");
            }
        }
    }

    /// <summary>
    /// Get unread notification count
    /// </summary>
    public async Task GetUnreadCount()
    {
        var userId = GetCurrentUserId();
        if (userId.HasValue)
        {
            try
            {
                var count = await _notificationService.GetUnreadCountAsync(userId.Value);
                await Clients.Caller.SendAsync("UnreadCount", count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread count for user {UserId}", userId);
                await Clients.Caller.SendAsync("Error", "Failed to get unread count");
            }
        }
    }

    /// <summary>
    /// Subscribe to push notifications
    /// </summary>
    public async Task SubscribeToPushNotifications(string endpoint, string p256dh, string auth, string platform)
    {
        var userId = GetCurrentUserId();
        if (userId.HasValue)
        {
            try
            {
                await _notificationService.CreateSubscriptionAsync(userId.Value, new DTOs.NotificationSubscriptionCreateDto
                {
                    Platform = platform,
                    Endpoint = endpoint,
                    P256dh = p256dh,
                    Auth = auth,
                    UserAgent = Context.GetHttpContext()?.Request.Headers.UserAgent.ToString()
                });

                await Clients.Caller.SendAsync("PushSubscriptionCreated");

                _logger.LogInformation("User {UserId} subscribed to push notifications on {Platform}",
                    userId, platform);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating push subscription for user {UserId}", userId);
                await Clients.Caller.SendAsync("Error", "Failed to create push subscription");
            }
        }
    }

    /// <summary>
    /// Update notification preferences
    /// </summary>
    public async Task UpdateNotificationPreferences(string notificationType, bool enablePush, bool enableEmail, bool enableSms)
    {
        var userId = GetCurrentUserId();
        if (userId.HasValue)
        {
            try
            {
                await _notificationService.UpdateUserPreferencesAsync(userId.Value, notificationType, new DTOs.UserNotificationPreferenceUpdateDto
                {
                    EnablePush = enablePush,
                    EnableEmail = enableEmail,
                    EnableSms = enableSms
                });

                await Clients.Caller.SendAsync("PreferencesUpdated", notificationType);

                _logger.LogInformation("User {UserId} updated preferences for {NotificationType}",
                    userId, notificationType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating preferences for user {UserId}", userId);
                await Clients.Caller.SendAsync("Error", "Failed to update preferences");
            }
        }
    }

    private Guid? GetCurrentUserId()
    {
        var objectIdClaim = Context.User?.FindFirst("oid")?.Value ??
                           Context.User?.FindFirst("sub")?.Value ??
                           Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(objectIdClaim, out var userId))
        {
            return userId;
        }

        return null;
    }

    private List<string> GetUserRoles()
    {
        return Context.User?.FindAll(ClaimTypes.Role)?.Select(c => c.Value).ToList() ?? new List<string>();
    }

    private async Task<bool> CanJoinGroup(Guid userId, string groupName)
    {
        // Implement authorization logic for groups
        // Examples:
        // - user_<userId> groups are always allowed
        // - order_<orderId> groups require order ownership verification
        // - role_<role> groups require role membership
        // - admin groups require admin role

        if (groupName.StartsWith($"user_{userId}"))
        {
            return true;
        }

        if (groupName.StartsWith("role_"))
        {
            var role = groupName.Substring(5);
            return Context.User?.IsInRole(role) ?? false;
        }

        if (groupName.StartsWith("order_"))
        {
            // In a real implementation, verify order ownership
            // var orderId = groupName.Substring(6);
            // return await _orderService.IsOrderOwnedByUser(orderId, userId);
            return true; // Simplified for this example
        }

        if (groupName.StartsWith("admin_"))
        {
            return Context.User?.IsInRole("Admin") ?? false;
        }

        // By default, don't allow joining arbitrary groups
        return false;
    }
}