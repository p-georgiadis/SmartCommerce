using Microsoft.EntityFrameworkCore;
using SmartCommerce.NotificationService.Models;

namespace SmartCommerce.NotificationService.Data;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<UserNotificationPreference> UserNotificationPreferences => Set<UserNotificationPreference>();
    public DbSet<NotificationSubscription> NotificationSubscriptions => Set<NotificationSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Notification entity
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.ScheduledAt);
            entity.HasIndex(e => new { e.UserId, e.IsRead });
            entity.HasIndex(e => new { e.UserId, e.Type });

            entity.Property(e => e.Metadata)
                  .HasColumnType("nvarchar(max)");
        });

        // Configure NotificationTemplate entity
        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.IsActive);

            entity.Property(e => e.MessageTemplate)
                  .HasColumnType("nvarchar(max)");
            entity.Property(e => e.EmailTemplate)
                  .HasColumnType("nvarchar(max)");
            entity.Property(e => e.SmsTemplate)
                  .HasColumnType("nvarchar(max)");
            entity.Property(e => e.PushTemplate)
                  .HasColumnType("nvarchar(max)");
            entity.Property(e => e.DefaultMetadata)
                  .HasColumnType("nvarchar(max)");
        });

        // Configure NotificationDelivery entity
        modelBuilder.Entity<NotificationDelivery>(entity =>
        {
            entity.HasOne(e => e.Notification)
                  .WithMany(e => e.Deliveries)
                  .HasForeignKey(e => e.NotificationId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.NotificationId);
            entity.HasIndex(e => e.Channel);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);

            entity.Property(e => e.Content)
                  .HasColumnType("nvarchar(max)");
            entity.Property(e => e.Response)
                  .HasColumnType("nvarchar(max)");
        });

        // Configure UserNotificationPreference entity
        modelBuilder.Entity<UserNotificationPreference>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.NotificationType }).IsUnique();
        });

        // Configure NotificationSubscription entity
        modelBuilder.Entity<NotificationSubscription>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Platform);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => new { e.UserId, e.Platform, e.DeviceId });
        });

        // Seed default notification templates
        SeedDefaultTemplates(modelBuilder);
    }

    private static void SeedDefaultTemplates(ModelBuilder modelBuilder)
    {
        var templates = new[]
        {
            new NotificationTemplate
            {
                Id = Guid.NewGuid(),
                Name = "order-created",
                Type = "order",
                Description = "Order creation confirmation",
                TitleTemplate = "Order Confirmed",
                MessageTemplate = "Your order #{OrderNumber} has been confirmed and is being processed.",
                EmailTemplate = @"
                    <h2>Order Confirmation</h2>
                    <p>Hello {CustomerName},</p>
                    <p>Your order #{OrderNumber} has been confirmed and is being processed.</p>
                    <p>Order Total: {OrderTotal}</p>
                    <p>Estimated delivery: {EstimatedDelivery}</p>
                    <p>Thank you for shopping with us!</p>",
                SmsTemplate = "Order #{OrderNumber} confirmed. Total: {OrderTotal}. Track: {TrackingUrl}",
                PushTemplate = "Order #{OrderNumber} confirmed",
                DefaultPriority = "high",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = Guid.Empty
            },
            new NotificationTemplate
            {
                Id = Guid.NewGuid(),
                Name = "order-status-update",
                Type = "order",
                Description = "Order status update notification",
                TitleTemplate = "Order Update",
                MessageTemplate = "Your order #{OrderNumber} status has been updated to {Status}.",
                EmailTemplate = @"
                    <h2>Order Status Update</h2>
                    <p>Hello {CustomerName},</p>
                    <p>Your order #{OrderNumber} status has been updated.</p>
                    <p>Current Status: {Status}</p>
                    <p>Track your order: <a href='{TrackingUrl}'>Click here</a></p>",
                SmsTemplate = "Order #{OrderNumber} is now {Status}. Track: {TrackingUrl}",
                PushTemplate = "Order #{OrderNumber} - {Status}",
                DefaultPriority = "normal",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = Guid.Empty
            },
            new NotificationTemplate
            {
                Id = Guid.NewGuid(),
                Name = "payment-failed",
                Type = "payment",
                Description = "Payment failure notification",
                TitleTemplate = "Payment Failed",
                MessageTemplate = "Payment for order #{OrderNumber} failed. Please update your payment method.",
                EmailTemplate = @"
                    <h2>Payment Failed</h2>
                    <p>Hello {CustomerName},</p>
                    <p>We were unable to process payment for order #{OrderNumber}.</p>
                    <p>Please update your payment method to complete the order.</p>
                    <p><a href='{UpdatePaymentUrl}'>Update Payment Method</a></p>",
                SmsTemplate = "Payment failed for order #{OrderNumber}. Please update payment method: {UpdatePaymentUrl}",
                PushTemplate = "Payment failed for order #{OrderNumber}",
                DefaultPriority = "high",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = Guid.Empty
            },
            new NotificationTemplate
            {
                Id = Guid.NewGuid(),
                Name = "welcome",
                Type = "system",
                Description = "Welcome new user",
                TitleTemplate = "Welcome to SmartCommerce!",
                MessageTemplate = "Welcome {FirstName}! Thank you for joining SmartCommerce. Start exploring our amazing products.",
                EmailTemplate = @"
                    <h2>Welcome to SmartCommerce!</h2>
                    <p>Hello {FirstName},</p>
                    <p>Thank you for joining SmartCommerce. We're excited to have you as part of our community!</p>
                    <p>Here are some things you can do to get started:</p>
                    <ul>
                        <li>Browse our product catalog</li>
                        <li>Set up your preferences</li>
                        <li>Add items to your wishlist</li>
                    </ul>
                    <p><a href='{ShopUrl}'>Start Shopping</a></p>",
                PushTemplate = "Welcome to SmartCommerce, {FirstName}!",
                DefaultPriority = "normal",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = Guid.Empty
            },
            new NotificationTemplate
            {
                Id = Guid.NewGuid(),
                Name = "security-alert",
                Type = "security",
                Description = "Security alert notification",
                TitleTemplate = "Security Alert",
                MessageTemplate = "Suspicious activity detected on your account. Please review your recent activity.",
                EmailTemplate = @"
                    <h2>Security Alert</h2>
                    <p>Hello {FirstName},</p>
                    <p>We detected suspicious activity on your account:</p>
                    <p>{ActivityDetails}</p>
                    <p>If this was not you, please secure your account immediately:</p>
                    <p><a href='{SecurityUrl}'>Secure Account</a></p>",
                SmsTemplate = "Security alert: Suspicious activity on your SmartCommerce account. Secure now: {SecurityUrl}",
                PushTemplate = "Security Alert - Please review your account",
                DefaultPriority = "critical",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = Guid.Empty
            }
        };

        modelBuilder.Entity<NotificationTemplate>().HasData(templates);
    }
}