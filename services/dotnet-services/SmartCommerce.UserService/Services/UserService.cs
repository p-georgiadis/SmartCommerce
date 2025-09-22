using AutoMapper;
using Microsoft.Extensions.Caching.Distributed;
using SmartCommerce.Shared.Messaging;
using SmartCommerce.UserService.DTOs;
using SmartCommerce.UserService.Models;
using SmartCommerce.UserService.Repositories;
using System.Text.Json;

namespace SmartCommerce.UserService.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    private readonly IDistributedCache _cache;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository userRepository,
        IMapper mapper,
        IDistributedCache cache,
        IMessagePublisher messagePublisher,
        ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _mapper = mapper;
        _cache = cache;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid id)
    {
        var cacheKey = $"user:{id}";
        var cachedUser = await _cache.GetStringAsync(cacheKey);

        if (!string.IsNullOrEmpty(cachedUser))
        {
            return JsonSerializer.Deserialize<UserDto>(cachedUser);
        }

        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) return null;

        var userDto = _mapper.Map<UserDto>(user);

        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
        };

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(userDto), cacheOptions);

        return userDto;
    }

    public async Task<UserDto?> GetUserByEmailAsync(string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        return user != null ? _mapper.Map<UserDto>(user) : null;
    }

    public async Task<UserDto?> GetUserByAzureAdB2CObjectIdAsync(string objectId)
    {
        var user = await _userRepository.GetByAzureAdB2CObjectIdAsync(objectId);
        return user != null ? _mapper.Map<UserDto>(user) : null;
    }

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync(int skip = 0, int take = 100)
    {
        var users = await _userRepository.GetAllAsync(skip, take);
        return _mapper.Map<IEnumerable<UserDto>>(users);
    }

    public async Task<IEnumerable<UserDto>> SearchUsersAsync(string searchTerm, int skip = 0, int take = 100)
    {
        var users = await _userRepository.SearchUsersAsync(searchTerm, skip, take);
        return _mapper.Map<IEnumerable<UserDto>>(users);
    }

    public async Task<UserDto> CreateUserAsync(UserCreateDto createDto)
    {
        if (await _userRepository.EmailExistsAsync(createDto.Email))
        {
            throw new InvalidOperationException($"User with email {createDto.Email} already exists");
        }

        var user = _mapper.Map<User>(createDto);
        var createdUser = await _userRepository.CreateAsync(user);

        // Assign default customer role
        var customerRole = await GetDefaultCustomerRoleAsync();
        if (customerRole != null)
        {
            var userRole = new UserRole
            {
                UserId = createdUser.Id,
                RoleId = customerRole.Id,
                AssignedBy = createdUser.Id // Self-assigned for new registrations
            };

            // Note: This would typically be handled by a UserRoleRepository
            // For simplicity, we're handling it here
        }

        var userDto = _mapper.Map<UserDto>(createdUser);

        // Clear cache and publish event
        await InvalidateUserCacheAsync(createdUser.Id);
        await PublishUserEventAsync("UserCreated", userDto);

        _logger.LogInformation("User created: {UserId} - {Email}", createdUser.Id, createdUser.Email);

        return userDto;
    }

    public async Task<UserDto> UpdateUserAsync(Guid id, UserUpdateDto updateDto)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID {id} not found");
        }

        _mapper.Map(updateDto, user);
        var updatedUser = await _userRepository.UpdateAsync(user);
        var userDto = _mapper.Map<UserDto>(updatedUser);

        // Clear cache and publish event
        await InvalidateUserCacheAsync(id);
        await PublishUserEventAsync("UserUpdated", userDto);

        _logger.LogInformation("User updated: {UserId}", id);

        return userDto;
    }

    public async Task DeleteUserAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID {id} not found");
        }

        await _userRepository.DeleteAsync(id);

        // Clear cache and publish event
        await InvalidateUserCacheAsync(id);
        await PublishUserEventAsync("UserDeleted", new { UserId = id, Email = user.Email });

        _logger.LogInformation("User deleted: {UserId}", id);
    }

    public async Task<bool> UserExistsAsync(Guid id)
    {
        return await _userRepository.ExistsAsync(id);
    }

    public async Task<bool> EmailExistsAsync(string email, Guid? excludeUserId = null)
    {
        return await _userRepository.EmailExistsAsync(email, excludeUserId);
    }

    public async Task<UserStatsDto> GetUserStatsAsync()
    {
        var cacheKey = "user:stats";
        var cachedStats = await _cache.GetStringAsync(cacheKey);

        if (!string.IsNullOrEmpty(cachedStats))
        {
            return JsonSerializer.Deserialize<UserStatsDto>(cachedStats) ?? new UserStatsDto();
        }

        var stats = await _userRepository.GetUserStatsAsync();

        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(stats), cacheOptions);

        return stats;
    }

    public async Task UpdateLastLoginAsync(Guid userId)
    {
        await _userRepository.UpdateLastLoginAsync(userId);
        await InvalidateUserCacheAsync(userId);

        _logger.LogInformation("User last login updated: {UserId}", userId);
    }

    public async Task<UserDto> SyncUserFromAzureAdB2CAsync(string objectId, UserCreateDto userDto)
    {
        var existingUser = await _userRepository.GetByAzureAdB2CObjectIdAsync(objectId);

        if (existingUser != null)
        {
            // Update existing user
            var updateDto = new UserUpdateDto
            {
                FirstName = userDto.FirstName,
                LastName = userDto.LastName,
                PhoneNumber = userDto.PhoneNumber,
                DateOfBirth = userDto.DateOfBirth,
                Gender = userDto.Gender,
                Bio = userDto.Bio
            };

            return await UpdateUserAsync(existingUser.Id, updateDto);
        }
        else
        {
            // Create new user
            return await CreateUserAsync(userDto);
        }
    }

    private async Task InvalidateUserCacheAsync(Guid userId)
    {
        var cacheKey = $"user:{userId}";
        await _cache.RemoveAsync(cacheKey);
        await _cache.RemoveAsync("user:stats");
    }

    private async Task PublishUserEventAsync(string eventType, object eventData)
    {
        try
        {
            await _messagePublisher.PublishAsync("user-events", eventType, eventData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish user event: {EventType}", eventType);
        }
    }

    private async Task<Role?> GetDefaultCustomerRoleAsync()
    {
        // This would typically be injected as IRoleRepository
        // For simplicity, returning null here
        // In a real implementation, you'd fetch the "Customer" role
        return null;
    }
}

public class UserAddressService : IUserAddressService
{
    private readonly IUserAddressRepository _addressRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<UserAddressService> _logger;

    public UserAddressService(
        IUserAddressRepository addressRepository,
        IUserRepository userRepository,
        IMapper mapper,
        ILogger<UserAddressService> logger)
    {
        _addressRepository = addressRepository;
        _userRepository = userRepository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<UserAddressDto?> GetAddressByIdAsync(Guid id)
    {
        var address = await _addressRepository.GetByIdAsync(id);
        return address != null ? _mapper.Map<UserAddressDto>(address) : null;
    }

    public async Task<IEnumerable<UserAddressDto>> GetUserAddressesAsync(Guid userId)
    {
        var addresses = await _addressRepository.GetByUserIdAsync(userId);
        return _mapper.Map<IEnumerable<UserAddressDto>>(addresses);
    }

    public async Task<UserAddressDto?> GetDefaultAddressAsync(Guid userId, string addressType)
    {
        var address = await _addressRepository.GetDefaultAddressAsync(userId, addressType);
        return address != null ? _mapper.Map<UserAddressDto>(address) : null;
    }

    public async Task<UserAddressDto> CreateAddressAsync(Guid userId, UserAddressCreateDto createDto)
    {
        if (!await _userRepository.ExistsAsync(userId))
        {
            throw new KeyNotFoundException($"User with ID {userId} not found");
        }

        var address = _mapper.Map<UserAddress>(createDto);
        address.UserId = userId;

        var createdAddress = await _addressRepository.CreateAsync(address);
        var addressDto = _mapper.Map<UserAddressDto>(createdAddress);

        _logger.LogInformation("Address created for user {UserId}: {AddressId}", userId, createdAddress.Id);

        return addressDto;
    }

    public async Task<UserAddressDto> UpdateAddressAsync(Guid id, UserAddressUpdateDto updateDto)
    {
        var address = await _addressRepository.GetByIdAsync(id);
        if (address == null)
        {
            throw new KeyNotFoundException($"Address with ID {id} not found");
        }

        _mapper.Map(updateDto, address);
        var updatedAddress = await _addressRepository.UpdateAsync(address);
        var addressDto = _mapper.Map<UserAddressDto>(updatedAddress);

        _logger.LogInformation("Address updated: {AddressId}", id);

        return addressDto;
    }

    public async Task DeleteAddressAsync(Guid id)
    {
        var address = await _addressRepository.GetByIdAsync(id);
        if (address == null)
        {
            throw new KeyNotFoundException($"Address with ID {id} not found");
        }

        await _addressRepository.DeleteAsync(id);

        _logger.LogInformation("Address deleted: {AddressId}", id);
    }

    public async Task SetDefaultAddressAsync(Guid userId, Guid addressId)
    {
        var address = await _addressRepository.GetByIdAsync(addressId);
        if (address == null || address.UserId != userId)
        {
            throw new KeyNotFoundException($"Address with ID {addressId} not found for user {userId}");
        }

        await _addressRepository.SetDefaultAddressAsync(userId, addressId, address.AddressType);

        _logger.LogInformation("Default address set for user {UserId}: {AddressId}", userId, addressId);
    }
}