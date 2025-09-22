using Microsoft.EntityFrameworkCore;
using SmartCommerce.UserService.Data;
using SmartCommerce.UserService.Models;
using SmartCommerce.UserService.DTOs;

namespace SmartCommerce.UserService.Repositories;

public class UserRepository : IUserRepository
{
    private readonly UserDbContext _context;

    public UserRepository(UserDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users
            .Include(u => u.Addresses)
            .Include(u => u.Preferences)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .Include(u => u.Addresses)
            .Include(u => u.Preferences)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User?> GetByAzureAdB2CObjectIdAsync(string objectId)
    {
        return await _context.Users
            .Include(u => u.Addresses)
            .Include(u => u.Preferences)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.AzureAdB2CObjectId == objectId);
    }

    public async Task<IEnumerable<User>> GetAllAsync(int skip = 0, int take = 100)
    {
        return await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .OrderBy(u => u.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<IEnumerable<User>> SearchUsersAsync(string searchTerm, int skip = 0, int take = 100)
    {
        var query = _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(u => u.FirstName.Contains(searchTerm) ||
                       u.LastName.Contains(searchTerm) ||
                       u.Email.Contains(searchTerm));

        return await query
            .OrderBy(u => u.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<User> CreateAsync(User user)
    {
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<User> UpdateAsync(User user)
    {
        user.UpdatedAt = DateTime.UtcNow;

        _context.Users.Update(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task DeleteAsync(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user != null)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Users.AnyAsync(u => u.Id == id);
    }

    public async Task<bool> EmailExistsAsync(string email, Guid? excludeUserId = null)
    {
        var query = _context.Users.Where(u => u.Email == email);

        if (excludeUserId.HasValue)
        {
            query = query.Where(u => u.Id != excludeUserId.Value);
        }

        return await query.AnyAsync();
    }

    public async Task<UserStatsDto> GetUserStatsAsync()
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var totalUsers = await _context.Users.CountAsync();
        var activeUsers = await _context.Users.CountAsync(u => u.IsActive);
        var newUsersToday = await _context.Users.CountAsync(u => u.CreatedAt >= today);
        var newUsersThisWeek = await _context.Users.CountAsync(u => u.CreatedAt >= weekStart);
        var newUsersThisMonth = await _context.Users.CountAsync(u => u.CreatedAt >= monthStart);

        var usersByCountry = await _context.Users
            .Join(_context.UserAddresses,
                  u => u.Id,
                  a => a.UserId,
                  (u, a) => new { User = u, Address = a })
            .Where(ua => ua.Address.AddressType == "Home")
            .GroupBy(ua => ua.Address.Country)
            .Select(g => new { Country = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Country, x => x.Count);

        var usersByRole = await _context.UserRoles
            .Include(ur => ur.Role)
            .GroupBy(ur => ur.Role.Name)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Role, x => x.Count);

        return new UserStatsDto
        {
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            NewUsersToday = newUsersToday,
            NewUsersThisWeek = newUsersThisWeek,
            NewUsersThisMonth = newUsersThisMonth,
            UsersByCountry = usersByCountry,
            UsersByRole = usersByRole
        };
    }

    public async Task UpdateLastLoginAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            user.LoginCount++;
            await _context.SaveChangesAsync();
        }
    }
}

public class UserAddressRepository : IUserAddressRepository
{
    private readonly UserDbContext _context;

    public UserAddressRepository(UserDbContext context)
    {
        _context = context;
    }

    public async Task<UserAddress?> GetByIdAsync(Guid id)
    {
        return await _context.UserAddresses
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IEnumerable<UserAddress>> GetByUserIdAsync(Guid userId)
    {
        return await _context.UserAddresses
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.AddressType)
            .ThenBy(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<UserAddress?> GetDefaultAddressAsync(Guid userId, string addressType)
    {
        return await _context.UserAddresses
            .FirstOrDefaultAsync(a => a.UserId == userId &&
                                     a.AddressType == addressType &&
                                     a.IsDefault);
    }

    public async Task<UserAddress> CreateAsync(UserAddress address)
    {
        address.CreatedAt = DateTime.UtcNow;
        address.UpdatedAt = DateTime.UtcNow;

        // If this is set as default, unset other defaults of the same type
        if (address.IsDefault)
        {
            await UnsetDefaultAddressesAsync(address.UserId, address.AddressType);
        }

        _context.UserAddresses.Add(address);
        await _context.SaveChangesAsync();
        return address;
    }

    public async Task<UserAddress> UpdateAsync(UserAddress address)
    {
        address.UpdatedAt = DateTime.UtcNow;

        // If this is set as default, unset other defaults of the same type
        if (address.IsDefault)
        {
            await UnsetDefaultAddressesAsync(address.UserId, address.AddressType, address.Id);
        }

        _context.UserAddresses.Update(address);
        await _context.SaveChangesAsync();
        return address;
    }

    public async Task DeleteAsync(Guid id)
    {
        var address = await _context.UserAddresses.FindAsync(id);
        if (address != null)
        {
            _context.UserAddresses.Remove(address);
            await _context.SaveChangesAsync();
        }
    }

    public async Task SetDefaultAddressAsync(Guid userId, Guid addressId, string addressType)
    {
        // Unset current default
        await UnsetDefaultAddressesAsync(userId, addressType);

        // Set new default
        var address = await _context.UserAddresses.FindAsync(addressId);
        if (address != null && address.UserId == userId && address.AddressType == addressType)
        {
            address.IsDefault = true;
            address.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    private async Task UnsetDefaultAddressesAsync(Guid userId, string addressType, Guid? excludeAddressId = null)
    {
        var addresses = await _context.UserAddresses
            .Where(a => a.UserId == userId &&
                       a.AddressType == addressType &&
                       a.IsDefault &&
                       (excludeAddressId == null || a.Id != excludeAddressId))
            .ToListAsync();

        foreach (var address in addresses)
        {
            address.IsDefault = false;
            address.UpdatedAt = DateTime.UtcNow;
        }

        if (addresses.Any())
        {
            await _context.SaveChangesAsync();
        }
    }
}