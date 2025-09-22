using Microsoft.EntityFrameworkCore;
using SmartCommerce.PaymentService.Models;
using System.Text.Json;

namespace SmartCommerce.PaymentService.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    public DbSet<Payment> Payments { get; set; }
    public DbSet<PaymentIntent> PaymentIntents { get; set; }
    public DbSet<Refund> Refunds { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Payment configuration
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderId).IsRequired();
            entity.Property(e => e.CustomerId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.RefundedAmount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
            entity.Property(e => e.TransactionId).HasMaxLength(100);
            entity.Property(e => e.ExternalTransactionId).HasMaxLength(200);
            entity.Property(e => e.ProviderPaymentId).HasMaxLength(200);
            entity.Property(e => e.FailureReason).HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.ReceiptUrl).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // JSON column for metadata
            entity.Property(e => e.Metadata)
                  .HasConversion(
                      v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                      v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>());

            // Indexes
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.TransactionId).IsUnique();
            entity.HasIndex(e => e.ExternalTransactionId);
            entity.HasIndex(e => e.ProviderPaymentId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.CustomerId, e.Status });
            entity.HasIndex(e => new { e.Provider, e.Status });
        });

        // PaymentIntent configuration
        modelBuilder.Entity<PaymentIntent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderId).IsRequired();
            entity.Property(e => e.CustomerId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
            entity.Property(e => e.ClientSecret).HasMaxLength(500);
            entity.Property(e => e.ProviderIntentId).HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // JSON column for metadata
            entity.Property(e => e.Metadata)
                  .HasConversion(
                      v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                      v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>());

            // Indexes
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ProviderIntentId);
            entity.HasIndex(e => e.ExpiresAt);
            entity.HasIndex(e => new { e.CustomerId, e.Status });
        });

        // Refund configuration
        modelBuilder.Entity<Refund>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PaymentId).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
            entity.Property(e => e.Reason).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.ExternalRefundId).HasMaxLength(200);
            entity.Property(e => e.FailureReason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // JSON column for metadata
            entity.Property(e => e.Metadata)
                  .HasConversion(
                      v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                      v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>());

            // Relationships
            entity.HasOne(e => e.Payment)
                  .WithMany()
                  .HasForeignKey(e => e.PaymentId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(e => e.PaymentId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ExternalRefundId);
            entity.HasIndex(e => e.CreatedAt);
        });
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var payments = ChangeTracker.Entries<Payment>()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in payments)
        {
            var entity = entry.Entity;
            if (entity.Status == PaymentStatus.Succeeded && entity.ProcessedAt == null)
            {
                entity.ProcessedAt = DateTime.UtcNow;
            }
            else if (entity.Status == PaymentStatus.Refunded && entity.RefundedAt == null)
            {
                entity.RefundedAt = DateTime.UtcNow;
            }
        }

        var intents = ChangeTracker.Entries<PaymentIntent>()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in intents)
        {
            var entity = entry.Entity;
            if (entity.Status == PaymentIntentStatus.Succeeded && entity.ConfirmedAt == null)
            {
                entity.ConfirmedAt = DateTime.UtcNow;
            }
        }

        var refunds = ChangeTracker.Entries<Refund>()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in refunds)
        {
            var entity = entry.Entity;
            if (entity.Status == RefundStatus.Succeeded && entity.ProcessedAt == null)
            {
                entity.ProcessedAt = DateTime.UtcNow;
            }
        }
    }
}