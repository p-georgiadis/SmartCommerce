using SmartCommerce.UserService.DTOs;
using SmartCommerce.UserService.Models;

namespace SmartCommerce.UserService.Services;

public interface IUserService
{
    Task<UserDto?> GetUserByIdAsync(Guid id);
    Task<UserDto?> GetUserByEmailAsync(string email);
    Task<UserDto?> GetUserByAzureAdB2CObjectIdAsync(string objectId);
    Task<IEnumerable<UserDto>> GetAllUsersAsync(int skip = 0, int take = 100);
    Task<IEnumerable<UserDto>> SearchUsersAsync(string searchTerm, int skip = 0, int take = 100);
    Task<UserDto> CreateUserAsync(UserCreateDto createDto);
    Task<UserDto> UpdateUserAsync(Guid id, UserUpdateDto updateDto);
    Task DeleteUserAsync(Guid id);
    Task<bool> UserExistsAsync(Guid id);
    Task<bool> EmailExistsAsync(string email, Guid? excludeUserId = null);
    Task<UserStatsDto> GetUserStatsAsync();
    Task UpdateLastLoginAsync(Guid userId);
    Task<UserDto> SyncUserFromAzureAdB2CAsync(string objectId, UserCreateDto userDto);
}

public interface IUserAddressService
{
    Task<UserAddressDto?> GetAddressByIdAsync(Guid id);
    Task<IEnumerable<UserAddressDto>> GetUserAddressesAsync(Guid userId);
    Task<UserAddressDto?> GetDefaultAddressAsync(Guid userId, string addressType);
    Task<UserAddressDto> CreateAddressAsync(Guid userId, UserAddressCreateDto createDto);
    Task<UserAddressDto> UpdateAddressAsync(Guid id, UserAddressUpdateDto updateDto);
    Task DeleteAddressAsync(Guid id);
    Task SetDefaultAddressAsync(Guid userId, Guid addressId);
}

public interface IUserPreferenceService
{
    Task<UserPreferenceDto?> GetPreferenceByIdAsync(Guid id);
    Task<IEnumerable<UserPreferenceDto>> GetUserPreferencesAsync(Guid userId);
    Task<UserPreferenceDto?> GetUserPreferenceAsync(Guid userId, string key);
    Task<UserPreferenceDto> SetUserPreferenceAsync(Guid userId, UserPreferenceCreateDto createDto);
    Task<UserPreferenceDto> UpdatePreferenceAsync(Guid id, UserPreferenceUpdateDto updateDto);
    Task DeletePreferenceAsync(Guid id);
    Task DeleteUserPreferenceAsync(Guid userId, string key);
}

public interface IRoleService
{
    Task<RoleDto?> GetRoleByIdAsync(Guid id);
    Task<RoleDto?> GetRoleByNameAsync(string name);
    Task<IEnumerable<RoleDto>> GetAllRolesAsync();
    Task<IEnumerable<RoleDto>> GetUserRolesAsync(Guid userId);
    Task<RoleDto> CreateRoleAsync(RoleCreateDto createDto);
    Task<RoleDto> UpdateRoleAsync(Guid id, RoleCreateDto updateDto);
    Task DeleteRoleAsync(Guid id);
    Task AssignRoleToUserAsync(Guid userId, Guid roleId, DateTime? expiresAt = null);
    Task RemoveRoleFromUserAsync(Guid userId, Guid roleId);
    Task<bool> UserHasRoleAsync(Guid userId, string roleName);
    Task<bool> UserHasPermissionAsync(Guid userId, string permissionName);
}

public interface IPermissionService
{
    Task<PermissionDto?> GetPermissionByIdAsync(Guid id);
    Task<PermissionDto?> GetPermissionByNameAsync(string name);
    Task<IEnumerable<PermissionDto>> GetAllPermissionsAsync();
    Task<IEnumerable<PermissionDto>> GetRolePermissionsAsync(Guid roleId);
    Task<IEnumerable<PermissionDto>> GetUserPermissionsAsync(Guid userId);
    Task AssignPermissionToRoleAsync(Guid roleId, Guid permissionId);
    Task RemovePermissionFromRoleAsync(Guid roleId, Guid permissionId);
}

public interface IAzureAdB2CService
{
    Task<UserDto?> GetUserFromAzureAdB2CAsync(string objectId);
    Task<bool> ValidateTokenAsync(string token);
    Task<string?> GetObjectIdFromTokenAsync(string token);
    Task UpdateUserInAzureAdB2CAsync(string objectId, UserUpdateDto updateDto);
    Task DeleteUserFromAzureAdB2CAsync(string objectId);
}