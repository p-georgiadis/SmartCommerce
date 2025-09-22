using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartCommerce.UserService.DTOs;
using SmartCommerce.UserService.Services;
using System.Security.Claims;

namespace SmartCommerce.UserService.Controllers;

[ApiController]
[Route("api/users/{userId:guid}/addresses")]
[Authorize]
public class UserAddressesController : ControllerBase
{
    private readonly IUserAddressService _addressService;
    private readonly IUserService _userService;
    private readonly ILogger<UserAddressesController> _logger;

    public UserAddressesController(
        IUserAddressService addressService,
        IUserService userService,
        ILogger<UserAddressesController> logger)
    {
        _addressService = addressService;
        _userService = userService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserAddressDto>>> GetUserAddresses(Guid userId)
    {
        if (!await CanAccessUserData(userId))
        {
            return Forbid("You can only access your own addresses");
        }

        var addresses = await _addressService.GetUserAddressesAsync(userId);
        return Ok(addresses);
    }

    [HttpGet("{addressId:guid}")]
    public async Task<ActionResult<UserAddressDto>> GetAddress(Guid userId, Guid addressId)
    {
        if (!await CanAccessUserData(userId))
        {
            return Forbid("You can only access your own addresses");
        }

        var address = await _addressService.GetAddressByIdAsync(addressId);
        if (address == null)
        {
            return NotFound($"Address with ID {addressId} not found");
        }

        return Ok(address);
    }

    [HttpGet("default/{addressType}")]
    public async Task<ActionResult<UserAddressDto>> GetDefaultAddress(Guid userId, string addressType)
    {
        if (!await CanAccessUserData(userId))
        {
            return Forbid("You can only access your own addresses");
        }

        var address = await _addressService.GetDefaultAddressAsync(userId, addressType);
        if (address == null)
        {
            return NotFound($"No default {addressType} address found for user {userId}");
        }

        return Ok(address);
    }

    [HttpPost]
    public async Task<ActionResult<UserAddressDto>> CreateAddress(
        Guid userId,
        [FromBody] UserAddressCreateDto createDto)
    {
        if (!await CanModifyUserData(userId))
        {
            return Forbid("You can only create addresses for your own account");
        }

        try
        {
            var address = await _addressService.CreateAddressAsync(userId, createDto);
            return CreatedAtAction(
                nameof(GetAddress),
                new { userId = userId, addressId = address.Id },
                address);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("{addressId:guid}")]
    public async Task<ActionResult<UserAddressDto>> UpdateAddress(
        Guid userId,
        Guid addressId,
        [FromBody] UserAddressUpdateDto updateDto)
    {
        if (!await CanModifyUserData(userId))
        {
            return Forbid("You can only update your own addresses");
        }

        try
        {
            var address = await _addressService.UpdateAddressAsync(addressId, updateDto);
            return Ok(address);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{addressId:guid}")]
    public async Task<IActionResult> DeleteAddress(Guid userId, Guid addressId)
    {
        if (!await CanModifyUserData(userId))
        {
            return Forbid("You can only delete your own addresses");
        }

        try
        {
            await _addressService.DeleteAddressAsync(addressId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("{addressId:guid}/set-default")]
    public async Task<IActionResult> SetDefaultAddress(Guid userId, Guid addressId)
    {
        if (!await CanModifyUserData(userId))
        {
            return Forbid("You can only modify your own addresses");
        }

        try
        {
            await _addressService.SetDefaultAddressAsync(userId, addressId);
            return Ok(new { Message = "Default address updated successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    private string? GetCurrentUserObjectId()
    {
        return User.FindFirstValue("oid") ??
               User.FindFirstValue("sub") ??
               User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private async Task<bool> CanAccessUserData(Guid userId)
    {
        // Admin can access any user's data
        if (User.IsInRole("Admin"))
        {
            return true;
        }

        // Users can only access their own data
        var objectId = GetCurrentUserObjectId();
        if (string.IsNullOrEmpty(objectId))
        {
            return false;
        }

        var currentUser = await _userService.GetUserByAzureAdB2CObjectIdAsync(objectId);
        return currentUser?.Id == userId;
    }

    private async Task<bool> CanModifyUserData(Guid userId)
    {
        // Same logic as CanAccessUserData for this controller
        return await CanAccessUserData(userId);
    }
}

[ApiController]
[Route("api/users/me/addresses")]
[Authorize]
public class MyAddressesController : ControllerBase
{
    private readonly IUserAddressService _addressService;
    private readonly IUserService _userService;
    private readonly ILogger<MyAddressesController> _logger;

    public MyAddressesController(
        IUserAddressService addressService,
        IUserService userService,
        ILogger<MyAddressesController> logger)
    {
        _addressService = addressService;
        _userService = userService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserAddressDto>>> GetMyAddresses()
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            return Unauthorized("Unable to determine user identity");
        }

        var addresses = await _addressService.GetUserAddressesAsync(userId.Value);
        return Ok(addresses);
    }

    [HttpGet("{addressId:guid}")]
    public async Task<ActionResult<UserAddressDto>> GetMyAddress(Guid addressId)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            return Unauthorized("Unable to determine user identity");
        }

        var address = await _addressService.GetAddressByIdAsync(addressId);
        if (address == null)
        {
            return NotFound($"Address with ID {addressId} not found");
        }

        return Ok(address);
    }

    [HttpGet("default/{addressType}")]
    public async Task<ActionResult<UserAddressDto>> GetMyDefaultAddress(string addressType)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            return Unauthorized("Unable to determine user identity");
        }

        var address = await _addressService.GetDefaultAddressAsync(userId.Value, addressType);
        if (address == null)
        {
            return NotFound($"No default {addressType} address found");
        }

        return Ok(address);
    }

    [HttpPost]
    public async Task<ActionResult<UserAddressDto>> CreateMyAddress([FromBody] UserAddressCreateDto createDto)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            return Unauthorized("Unable to determine user identity");
        }

        try
        {
            var address = await _addressService.CreateAddressAsync(userId.Value, createDto);
            return CreatedAtAction(
                nameof(GetMyAddress),
                new { addressId = address.Id },
                address);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("{addressId:guid}")]
    public async Task<ActionResult<UserAddressDto>> UpdateMyAddress(
        Guid addressId,
        [FromBody] UserAddressUpdateDto updateDto)
    {
        try
        {
            var address = await _addressService.UpdateAddressAsync(addressId, updateDto);
            return Ok(address);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{addressId:guid}")]
    public async Task<IActionResult> DeleteMyAddress(Guid addressId)
    {
        try
        {
            await _addressService.DeleteAddressAsync(addressId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("{addressId:guid}/set-default")]
    public async Task<IActionResult> SetMyDefaultAddress(Guid addressId)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null)
        {
            return Unauthorized("Unable to determine user identity");
        }

        try
        {
            await _addressService.SetDefaultAddressAsync(userId.Value, addressId);
            return Ok(new { Message = "Default address updated successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    private string? GetCurrentUserObjectId()
    {
        return User.FindFirstValue("oid") ??
               User.FindFirstValue("sub") ??
               User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private async Task<Guid?> GetCurrentUserIdAsync()
    {
        var objectId = GetCurrentUserObjectId();
        if (string.IsNullOrEmpty(objectId))
        {
            return null;
        }

        var currentUser = await _userService.GetUserByAzureAdB2CObjectIdAsync(objectId);
        return currentUser?.Id;
    }
}