using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartCommerce.NotificationService.DTOs;
using SmartCommerce.NotificationService.Services;
using System.Security.Claims;

namespace SmartCommerce.NotificationService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationService notificationService,
        ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NotificationDto>> GetNotification(Guid id)
    {
        var notification = await _notificationService.GetNotificationAsync(id);
        if (notification == null)
        {
            return NotFound($"Notification with ID {id} not found");
        }

        // Check if user can access this notification
        var currentUserId = GetCurrentUserId();
        if (currentUserId != notification.UserId && !User.IsInRole("Admin"))
        {
            return Forbid("You can only access your own notifications");
        }

        return Ok(notification);
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<ActionResult<List<NotificationDto>>> GetUserNotifications(
        Guid userId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        [FromQuery] string? type = null,
        [FromQuery] bool? isRead = null)
    {
        // Check authorization
        var currentUserId = GetCurrentUserId();
        if (currentUserId != userId && !User.IsInRole("Admin"))
        {
            return Forbid("You can only access your own notifications");
        }

        var notifications = await _notificationService.GetUserNotificationsAsync(userId, skip, take, type, isRead);
        return Ok(notifications);
    }

    [HttpGet("me")]
    public async Task<ActionResult<List<NotificationDto>>> GetMyNotifications(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        [FromQuery] string? type = null,
        [FromQuery] bool? isRead = null)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
        {
            return Unauthorized("Unable to determine user identity");
        }

        var notifications = await _notificationService.GetUserNotificationsAsync(currentUserId.Value, skip, take, type, isRead);
        return Ok(notifications);
    }

    [HttpGet("me/unread-count")]
    public async Task<ActionResult<int>> GetMyUnreadCount()
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
        {
            return Unauthorized("Unable to determine user identity");
        }

        var count = await _notificationService.GetUnreadCountAsync(currentUserId.Value);
        return Ok(count);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,System")]
    public async Task<ActionResult<NotificationDto>> CreateNotification([FromBody] NotificationCreateDto createDto)
    {
        try
        {
            var notification = await _notificationService.CreateNotificationAsync(createDto);
            return CreatedAtAction(nameof(GetNotification), new { id = notification.Id }, notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create notification");
            return StatusCode(500, "Failed to create notification");
        }
    }

    [HttpPost("bulk")]
    [Authorize(Roles = "Admin,System")]
    public async Task<ActionResult<List<NotificationDto>>> CreateBulkNotifications([FromBody] BulkNotificationCreateDto createDto)
    {
        try
        {
            var notifications = await _notificationService.CreateBulkNotificationAsync(createDto);
            return Ok(notifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create bulk notifications");
            return StatusCode(500, "Failed to create bulk notifications");
        }
    }

    [HttpPost("from-template")]
    [Authorize(Roles = "Admin,System")]
    public async Task<ActionResult<NotificationDto>> CreateFromTemplate([FromBody] TemplateNotificationCreateDto createDto)
    {
        try
        {
            var notification = await _notificationService.CreateFromTemplateAsync(createDto);
            return CreatedAtAction(nameof(GetNotification), new { id = notification.Id }, notification);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create notification from template");
            return StatusCode(500, "Failed to create notification from template");
        }
    }

    [HttpPut("{id:guid}/mark-read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var notification = await _notificationService.GetNotificationAsync(id);
        if (notification == null)
        {
            return NotFound($"Notification with ID {id} not found");
        }

        // Check authorization
        var currentUserId = GetCurrentUserId();
        if (currentUserId != notification.UserId && !User.IsInRole("Admin"))
        {
            return Forbid("You can only mark your own notifications as read");
        }

        await _notificationService.MarkNotificationAsReadAsync(id);
        return Ok(new { Message = "Notification marked as read" });
    }

    [HttpPut("mark-read")]
    public async Task<IActionResult> MarkMultipleAsRead([FromBody] MarkAsReadDto markAsReadDto)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
        {
            return Unauthorized("Unable to determine user identity");
        }

        await _notificationService.MarkNotificationsAsReadAsync(currentUserId.Value, markAsReadDto.NotificationIds);
        return Ok(new { Message = "Notifications marked as read" });
    }

    [HttpPut("me/mark-all-read")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
        {
            return Unauthorized("Unable to determine user identity");
        }

        await _notificationService.MarkAllAsReadAsync(currentUserId.Value);
        return Ok(new { Message = "All notifications marked as read" });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteNotification(Guid id)
    {
        var notification = await _notificationService.GetNotificationAsync(id);
        if (notification == null)
        {
            return NotFound($"Notification with ID {id} not found");
        }

        // Check authorization
        var currentUserId = GetCurrentUserId();
        if (currentUserId != notification.UserId && !User.IsInRole("Admin"))
        {
            return Forbid("You can only delete your own notifications");
        }

        await _notificationService.DeleteNotificationAsync(id);
        return NoContent();
    }

    [HttpDelete("me")]
    public async Task<IActionResult> DeleteMyNotifications([FromBody] List<Guid> notificationIds)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
        {
            return Unauthorized("Unable to determine user identity");
        }

        await _notificationService.DeleteUserNotificationsAsync(currentUserId.Value, notificationIds);
        return NoContent();
    }

    [HttpPost("{id:guid}/send")]
    [Authorize(Roles = "Admin,System")]
    public async Task<IActionResult> SendNotification(Guid id)
    {
        var notification = await _notificationService.GetNotificationAsync(id);
        if (notification == null)
        {
            return NotFound($"Notification with ID {id} not found");
        }

        try
        {
            await _notificationService.SendNotificationAsync(id);
            return Ok(new { Message = "Notification sent" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification {NotificationId}", id);
            return StatusCode(500, "Failed to send notification");
        }
    }

    [HttpGet("stats")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<NotificationStatsDto>> GetNotificationStats([FromQuery] Guid? userId = null)
    {
        var stats = await _notificationService.GetNotificationStatsAsync(userId);
        return Ok(stats);
    }

    [HttpGet("me/stats")]
    public async Task<ActionResult<NotificationStatsDto>> GetMyNotificationStats()
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
        {
            return Unauthorized("Unable to determine user identity");
        }

        var stats = await _notificationService.GetNotificationStatsAsync(currentUserId.Value);
        return Ok(stats);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue("sub") ??
                         User.FindFirstValue("oid") ??
                         User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

[ApiController]
[Route("api/notification-templates")]
[Authorize(Roles = "Admin")]
public class NotificationTemplatesController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationTemplatesController> _logger;

    public NotificationTemplatesController(
        INotificationService notificationService,
        ILogger<NotificationTemplatesController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<NotificationTemplateDto>>> GetTemplates()
    {
        var templates = await _notificationService.GetTemplatesAsync();
        return Ok(templates);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NotificationTemplateDto>> GetTemplate(Guid id)
    {
        var template = await _notificationService.GetTemplateAsync(id);
        if (template == null)
        {
            return NotFound($"Template with ID {id} not found");
        }

        return Ok(template);
    }

    [HttpGet("by-name/{name}")]
    public async Task<ActionResult<NotificationTemplateDto>> GetTemplateByName(string name)
    {
        var template = await _notificationService.GetTemplateByNameAsync(name);
        if (template == null)
        {
            return NotFound($"Template with name '{name}' not found");
        }

        return Ok(template);
    }

    [HttpPost]
    public async Task<ActionResult<NotificationTemplateDto>> CreateTemplate([FromBody] NotificationTemplateCreateDto createDto)
    {
        try
        {
            var template = await _notificationService.CreateTemplateAsync(createDto);
            return CreatedAtAction(nameof(GetTemplate), new { id = template.Id }, template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create notification template");
            return StatusCode(500, "Failed to create notification template");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NotificationTemplateDto>> UpdateTemplate(Guid id, [FromBody] NotificationTemplateCreateDto updateDto)
    {
        try
        {
            var template = await _notificationService.UpdateTemplateAsync(id, updateDto);
            return Ok(template);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update notification template {TemplateId}", id);
            return StatusCode(500, "Failed to update notification template");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTemplate(Guid id)
    {
        var template = await _notificationService.GetTemplateAsync(id);
        if (template == null)
        {
            return NotFound($"Template with ID {id} not found");
        }

        await _notificationService.DeleteTemplateAsync(id);
        return NoContent();
    }
}