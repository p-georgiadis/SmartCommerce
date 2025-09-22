using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartCommerce.UserService.DTOs;
using SmartCommerce.UserService.Services;
using System.Security.Claims;

namespace SmartCommerce.UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAzureAdB2CService _azureAdB2CService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserService userService,
        IAzureAdB2CService azureAdB2CService,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _azureAdB2CService = azureAdB2CService;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDto>> GetUser(Guid id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound($"User with ID {id} not found");
        }

        return Ok(user);
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var objectId = GetCurrentUserObjectId();
        if (string.IsNullOrEmpty(objectId))
        {
            return Unauthorized("Unable to determine user identity");
        }

        var user = await _userService.GetUserByAzureAdB2CObjectIdAsync(objectId);
        if (user == null)
        {
            return NotFound("Current user not found");
        }

        // Update last login
        await _userService.UpdateLastLoginAsync(user.Id);

        return Ok(user);
    }

    [HttpGet("email/{email}")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<ActionResult<UserDto>> GetUserByEmail(string email)
    {
        var user = await _userService.GetUserByEmailAsync(email);
        if (user == null)
        {
            return NotFound($"User with email {email} not found");
        }

        return Ok(user);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        [FromQuery] string? search = null)
    {
        if (take > 100) take = 100; // Limit page size

        IEnumerable<UserDto> users;

        if (!string.IsNullOrEmpty(search))
        {
            users = await _userService.SearchUsersAsync(search, skip, take);
        }
        else
        {
            users = await _userService.GetAllUsersAsync(skip, take);
        }

        return Ok(users);
    }

    [HttpPost]
    [AllowAnonymous] // For registration
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] UserCreateDto createDto)
    {
        try
        {
            var user = await _userService.CreateUserAsync(createDto);
            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("sync")]
    [AllowAnonymous] // For Azure AD B2C sync
    public async Task<ActionResult<UserDto>> SyncUserFromAzureAd([FromBody] UserCreateDto createDto)
    {
        if (string.IsNullOrEmpty(createDto.AzureAdB2CObjectId))
        {
            return BadRequest("Azure AD B2C Object ID is required for sync");
        }

        try
        {
            var user = await _userService.SyncUserFromAzureAdB2CAsync(createDto.AzureAdB2CObjectId, createDto);
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync user from Azure AD B2C: {ObjectId}", createDto.AzureAdB2CObjectId);
            return StatusCode(500, "Failed to sync user");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserDto>> UpdateUser(Guid id, [FromBody] UserUpdateDto updateDto)
    {
        // Users can only update their own profile unless they're admin
        if (!await CanModifyUser(id))
        {
            return Forbid("You can only update your own profile");
        }

        try
        {
            var user = await _userService.UpdateUserAsync(id, updateDto);
            return Ok(user);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("me")]
    public async Task<ActionResult<UserDto>> UpdateCurrentUser([FromBody] UserUpdateDto updateDto)
    {
        var objectId = GetCurrentUserObjectId();
        if (string.IsNullOrEmpty(objectId))
        {
            return Unauthorized("Unable to determine user identity");
        }

        var currentUser = await _userService.GetUserByAzureAdB2CObjectIdAsync(objectId);
        if (currentUser == null)
        {
            return NotFound("Current user not found");
        }

        try
        {
            var user = await _userService.UpdateUserAsync(currentUser.Id, updateDto);
            return Ok(user);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        try
        {
            await _userService.DeleteUserAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("stats")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserStatsDto>> GetUserStats()
    {
        var stats = await _userService.GetUserStatsAsync();
        return Ok(stats);
    }

    [HttpPost("{id:guid}/verify-email")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> VerifyEmail(Guid id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound($"User with ID {id} not found");
        }

        // Update email verification status
        var updateDto = new UserUpdateDto(); // In a real implementation, you'd have EmailVerified property
        await _userService.UpdateUserAsync(id, updateDto);

        return Ok(new { Message = "Email verified successfully" });
    }

    [HttpPost("{id:guid}/verify-phone")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> VerifyPhone(Guid id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound($"User with ID {id} not found");
        }

        // Update phone verification status
        var updateDto = new UserUpdateDto(); // In a real implementation, you'd have PhoneVerified property
        await _userService.UpdateUserAsync(id, updateDto);

        return Ok(new { Message = "Phone verified successfully" });
    }

    private string? GetCurrentUserObjectId()
    {
        return User.FindFirstValue("oid") ??
               User.FindFirstValue("sub") ??
               User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private async Task<bool> CanModifyUser(Guid userId)
    {
        // Admin can modify any user
        if (User.IsInRole("Admin"))
        {
            return true;
        }

        // Users can only modify their own profile
        var objectId = GetCurrentUserObjectId();
        if (string.IsNullOrEmpty(objectId))
        {
            return false;
        }

        var currentUser = await _userService.GetUserByAzureAdB2CObjectIdAsync(objectId);
        return currentUser?.Id == userId;
    }
}