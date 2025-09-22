using System.ComponentModel.DataAnnotations;

namespace SmartCommerce.UserService.DTOs;

public record UserCreateDto
{
    [Required]
    [StringLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; init; }

    [Required]
    public string AzureAdB2CObjectId { get; init; } = string.Empty;

    public DateTime? DateOfBirth { get; init; }

    public string? Gender { get; init; }

    public string? Bio { get; init; }
}

public record UserUpdateDto
{
    [StringLength(100)]
    public string? FirstName { get; init; }

    [StringLength(100)]
    public string? LastName { get; init; }

    [Phone]
    public string? PhoneNumber { get; init; }

    public DateTime? DateOfBirth { get; init; }

    public string? Gender { get; init; }

    public string? Bio { get; init; }

    public string? ProfileImageUrl { get; init; }
}

public record UserDto
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public string AzureAdB2CObjectId { get; init; } = string.Empty;
    public DateTime DateOfBirth { get; init; }
    public string? Gender { get; init; }
    public string? Bio { get; init; }
    public string? ProfileImageUrl { get; init; }
    public bool IsActive { get; init; }
    public bool EmailVerified { get; init; }
    public bool PhoneVerified { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public int LoginCount { get; init; }
    public List<UserAddressDto> Addresses { get; init; } = new();
    public List<UserPreferenceDto> Preferences { get; init; } = new();
    public List<string> Roles { get; init; } = new();
}

public record UserAddressCreateDto
{
    [Required]
    public string AddressType { get; init; } = string.Empty;

    [Required]
    public string AddressLine1 { get; init; } = string.Empty;

    public string? AddressLine2 { get; init; }

    [Required]
    public string City { get; init; } = string.Empty;

    [Required]
    public string State { get; init; } = string.Empty;

    [Required]
    public string PostalCode { get; init; } = string.Empty;

    [Required]
    public string Country { get; init; } = string.Empty;

    public bool IsDefault { get; init; }
}

public record UserAddressUpdateDto
{
    public string? AddressType { get; init; }
    public string? AddressLine1 { get; init; }
    public string? AddressLine2 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public bool? IsDefault { get; init; }
}

public record UserAddressDto
{
    public Guid Id { get; init; }
    public string AddressType { get; init; } = string.Empty;
    public string AddressLine1 { get; init; } = string.Empty;
    public string? AddressLine2 { get; init; }
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record UserPreferenceCreateDto
{
    [Required]
    public string PreferenceKey { get; init; } = string.Empty;

    [Required]
    public string PreferenceValue { get; init; } = string.Empty;

    public string? Category { get; init; }
}

public record UserPreferenceUpdateDto
{
    public string? PreferenceValue { get; init; }
    public string? Category { get; init; }
}

public record UserPreferenceDto
{
    public Guid Id { get; init; }
    public string PreferenceKey { get; init; } = string.Empty;
    public string PreferenceValue { get; init; } = string.Empty;
    public string? Category { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record RoleDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public List<string> Permissions { get; init; } = new();
}

public record RoleCreateDto
{
    [Required]
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public List<Guid> PermissionIds { get; init; } = new();
}

public record PermissionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Resource { get; init; }
    public string? Action { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record UserRoleAssignmentDto
{
    [Required]
    public Guid UserId { get; init; }

    [Required]
    public Guid RoleId { get; init; }

    public DateTime? ExpiresAt { get; init; }
}

public record UserStatsDto
{
    public int TotalUsers { get; init; }
    public int ActiveUsers { get; init; }
    public int NewUsersToday { get; init; }
    public int NewUsersThisWeek { get; init; }
    public int NewUsersThisMonth { get; init; }
    public Dictionary<string, int> UsersByCountry { get; init; } = new();
    public Dictionary<string, int> UsersByRole { get; init; } = new();
}