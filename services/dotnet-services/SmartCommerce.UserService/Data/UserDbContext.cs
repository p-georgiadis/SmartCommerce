using Microsoft.EntityFrameworkCore;
using SmartCommerce.UserService.Models;

namespace SmartCommerce.UserService.Data;

public class UserDbContext : DbContext
{
    public UserDbContext(DbContextOptions<UserDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserAddress> UserAddresses => Set<UserAddress>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.AzureAdB2CObjectId).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.AzureAdB2CObjectId).HasMaxLength(256);
        });

        // Configure UserAddress entity
        modelBuilder.Entity<UserAddress>(entity =>
        {
            entity.HasOne(e => e.User)
                  .WithMany(e => e.Addresses)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.UserId, e.AddressType });
        });

        // Configure UserPreference entity
        modelBuilder.Entity<UserPreference>(entity =>
        {
            entity.HasOne(e => e.User)
                  .WithMany(e => e.Preferences)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.UserId, e.PreferenceKey }).IsUnique();
        });

        // Configure Role entity
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Configure UserRole entity
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasOne(e => e.User)
                  .WithMany(e => e.UserRoles)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Role)
                  .WithMany(e => e.UserRoles)
                  .HasForeignKey(e => e.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.UserId, e.RoleId }).IsUnique();
        });

        // Configure Permission entity
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => new { e.Resource, e.Action });
        });

        // Configure RolePermission entity
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasOne(e => e.Role)
                  .WithMany(e => e.RolePermissions)
                  .HasForeignKey(e => e.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Permission)
                  .WithMany(e => e.RolePermissions)
                  .HasForeignKey(e => e.PermissionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.RoleId, e.PermissionId }).IsUnique();
        });

        // Seed data
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // Seed default roles
        var adminRoleId = Guid.NewGuid();
        var customerRoleId = Guid.NewGuid();
        var moderatorRoleId = Guid.NewGuid();

        modelBuilder.Entity<Role>().HasData(
            new Role
            {
                Id = adminRoleId,
                Name = "Admin",
                Description = "Full system access",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new Role
            {
                Id = customerRoleId,
                Name = "Customer",
                Description = "Standard customer access",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new Role
            {
                Id = moderatorRoleId,
                Name = "Moderator",
                Description = "Content moderation access",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        );

        // Seed default permissions
        var permissions = new[]
        {
            new Permission { Id = Guid.NewGuid(), Name = "users.read", Description = "Read user information", Resource = "users", Action = "read", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Permission { Id = Guid.NewGuid(), Name = "users.write", Description = "Create and update users", Resource = "users", Action = "write", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Permission { Id = Guid.NewGuid(), Name = "users.delete", Description = "Delete users", Resource = "users", Action = "delete", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Permission { Id = Guid.NewGuid(), Name = "orders.read", Description = "Read order information", Resource = "orders", Action = "read", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Permission { Id = Guid.NewGuid(), Name = "orders.write", Description = "Create and update orders", Resource = "orders", Action = "write", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Permission { Id = Guid.NewGuid(), Name = "products.read", Description = "Read product information", Resource = "products", Action = "read", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Permission { Id = Guid.NewGuid(), Name = "products.write", Description = "Create and update products", Resource = "products", Action = "write", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Permission { Id = Guid.NewGuid(), Name = "admin.access", Description = "Administrative access", Resource = "admin", Action = "access", IsActive = true, CreatedAt = DateTime.UtcNow }
        };

        modelBuilder.Entity<Permission>().HasData(permissions);

        // Seed role permissions
        var rolePermissions = new List<RolePermission>();

        // Admin gets all permissions
        foreach (var permission in permissions)
        {
            rolePermissions.Add(new RolePermission
            {
                Id = Guid.NewGuid(),
                RoleId = adminRoleId,
                PermissionId = permission.Id,
                AssignedAt = DateTime.UtcNow
            });
        }

        // Customer gets basic permissions
        var customerPermissions = permissions.Where(p =>
            p.Name.Contains("orders.read") ||
            p.Name.Contains("orders.write") ||
            p.Name.Contains("products.read") ||
            p.Name.Contains("users.read")).ToArray();

        foreach (var permission in customerPermissions)
        {
            rolePermissions.Add(new RolePermission
            {
                Id = Guid.NewGuid(),
                RoleId = customerRoleId,
                PermissionId = permission.Id,
                AssignedAt = DateTime.UtcNow
            });
        }

        // Moderator gets content management permissions
        var moderatorPermissions = permissions.Where(p =>
            p.Name.Contains("products") ||
            p.Name.Contains("users.read") ||
            p.Name.Contains("orders.read")).ToArray();

        foreach (var permission in moderatorPermissions)
        {
            rolePermissions.Add(new RolePermission
            {
                Id = Guid.NewGuid(),
                RoleId = moderatorRoleId,
                PermissionId = permission.Id,
                AssignedAt = DateTime.UtcNow
            });
        }

        modelBuilder.Entity<RolePermission>().HasData(rolePermissions);
    }
}