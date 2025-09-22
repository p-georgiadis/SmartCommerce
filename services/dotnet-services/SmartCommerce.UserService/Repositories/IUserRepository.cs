using SmartCommerce.UserService.Models;
using SmartCommerce.UserService.DTOs;

namespace SmartCommerce.UserService.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByAzureAdB2CObjectIdAsync(string objectId);
    Task<IEnumerable<User>> GetAllAsync(int skip = 0, int take = 100);
    Task<IEnumerable<User>> SearchUsersAsync(string searchTerm, int skip = 0, int take = 100);
    Task<User> CreateAsync(User user);
    Task<User> UpdateAsync(User user);
    Task DeleteAsync(Guid id);
    Task<bool> ExistsAsync(Guid id);
    Task<bool> EmailExistsAsync(string email, Guid? excludeUserId = null);
    Task<UserStatsDto> GetUserStatsAsync();
    Task UpdateLastLoginAsync(Guid userId);
}

public interface IUserAddressRepository
{
    Task<UserAddress?> GetByIdAsync(Guid id);
    Task<IEnumerable<UserAddress>> GetByUserIdAsync(Guid userId);
    Task<UserAddress?> GetDefaultAddressAsync(Guid userId, string addressType);
    Task<UserAddress> CreateAsync(UserAddress address);
    Task<UserAddress> UpdateAsync(UserAddress address);
    Task DeleteAsync(Guid id);
    Task SetDefaultAddressAsync(Guid userId, Guid addressId, string addressType);
}

public interface IUserPreferenceRepository
{
    Task<UserPreference?> GetByIdAsync(Guid id);
    Task<IEnumerable<UserPreference>> GetByUserIdAsync(Guid userId);
    Task<UserPreference?> GetByUserIdAndKeyAsync(Guid userId, string key);
    Task<UserPreference> CreateAsync(UserPreference preference);
    Task<UserPreference> UpdateAsync(UserPreference preference);
    Task DeleteAsync(Guid id);
    Task DeleteByUserIdAndKeyAsync(Guid userId, string key);
}

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid id);
    Task<Role?> GetByNameAsync(string name);
    Task<IEnumerable<Role>> GetAllAsync();
    Task<IEnumerable<Role>> GetByUserIdAsync(Guid userId);
    Task<Role> CreateAsync(Role role);
    Task<Role> UpdateAsync(Role role);
    Task DeleteAsync(Guid id);
    Task<bool> ExistsAsync(Guid id);
    Task<bool> NameExistsAsync(string name, Guid? excludeRoleId = null);
}

public interface IPermissionRepository
{
    Task<Permission?> GetByIdAsync(Guid id);
    Task<Permission?> GetByNameAsync(string name);
    Task<IEnumerable<Permission>> GetAllAsync();
    Task<IEnumerable<Permission>> GetByRoleIdAsync(Guid roleId);
    Task<IEnumerable<Permission>> GetByUserIdAsync(Guid userId);
    Task<Permission> CreateAsync(Permission permission);
    Task<Permission> UpdateAsync(Permission permission);
    Task DeleteAsync(Guid id);
    Task<bool> ExistsAsync(Guid id);
    Task<bool> NameExistsAsync(string name, Guid? excludePermissionId = null);
}

public interface IUserRoleRepository
{
    Task<UserRole?> GetByIdAsync(Guid id);
    Task<IEnumerable<UserRole>> GetByUserIdAsync(Guid userId);
    Task<IEnumerable<UserRole>> GetByRoleIdAsync(Guid roleId);
    Task<UserRole?> GetByUserIdAndRoleIdAsync(Guid userId, Guid roleId);
    Task<UserRole> CreateAsync(UserRole userRole);
    Task DeleteAsync(Guid id);
    Task DeleteByUserIdAndRoleIdAsync(Guid userId, Guid roleId);
    Task<bool> HasRoleAsync(Guid userId, string roleName);
    Task<bool> HasPermissionAsync(Guid userId, string permissionName);
}