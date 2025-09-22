using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Authentication;
using Azure.Identity;
using SmartCommerce.UserService.DTOs;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace SmartCommerce.UserService.Services;

public class AzureAdB2CService : IAzureAdB2CService
{
    private readonly GraphServiceClient _graphServiceClient;
    private readonly AzureAdB2COptions _options;
    private readonly ILogger<AzureAdB2CService> _logger;

    public AzureAdB2CService(
        IOptions<AzureAdB2COptions> options,
        ILogger<AzureAdB2CService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var clientCredential = new ClientSecretCredential(
            _options.TenantId,
            _options.ClientId,
            _options.ClientSecret);

        var authProvider = new BaseBearerTokenAuthenticationProvider(
            new TokenCredential[] { clientCredential });

        _graphServiceClient = new GraphServiceClient(authProvider);
    }

    public async Task<UserDto?> GetUserFromAzureAdB2CAsync(string objectId)
    {
        try
        {
            var user = await _graphServiceClient.Users[objectId].GetAsync();

            if (user == null) return null;

            return new UserDto
            {
                Id = Guid.Parse(user.Id!),
                FirstName = user.GivenName ?? string.Empty,
                LastName = user.Surname ?? string.Empty,
                Email = user.Mail ?? user.UserPrincipalName ?? string.Empty,
                PhoneNumber = user.MobilePhone,
                AzureAdB2CObjectId = user.Id!,
                EmailVerified = user.Mail != null,
                IsActive = user.AccountEnabled ?? false,
                CreatedAt = user.CreatedDateTime?.DateTime ?? DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user from Azure AD B2C: {ObjectId}", objectId);
            return null;
        }
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);

            // Basic validation - in production, you'd validate signature, issuer, audience, etc.
            var now = DateTime.UtcNow;
            if (jwtToken.ValidTo < now || jwtToken.ValidFrom > now)
            {
                return false;
            }

            // Validate issuer
            var expectedIssuer = $"https://{_options.Domain}/v2.0/";
            if (!jwtToken.Issuer.StartsWith(expectedIssuer))
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate token");
            return false;
        }
    }

    public async Task<string?> GetObjectIdFromTokenAsync(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);

            var objectIdClaim = jwtToken.Claims.FirstOrDefault(c =>
                c.Type == "oid" || c.Type == "sub" || c.Type == ClaimTypes.NameIdentifier);

            return objectIdClaim?.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract object ID from token");
            return null;
        }
    }

    public async Task UpdateUserInAzureAdB2CAsync(string objectId, UserUpdateDto updateDto)
    {
        try
        {
            var user = new Microsoft.Graph.Models.User
            {
                GivenName = updateDto.FirstName,
                Surname = updateDto.LastName,
                MobilePhone = updateDto.PhoneNumber
            };

            await _graphServiceClient.Users[objectId].PatchAsync(user);

            _logger.LogInformation("User updated in Azure AD B2C: {ObjectId}", objectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user in Azure AD B2C: {ObjectId}", objectId);
            throw;
        }
    }

    public async Task DeleteUserFromAzureAdB2CAsync(string objectId)
    {
        try
        {
            await _graphServiceClient.Users[objectId].DeleteAsync();

            _logger.LogInformation("User deleted from Azure AD B2C: {ObjectId}", objectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete user from Azure AD B2C: {ObjectId}", objectId);
            throw;
        }
    }
}

public class AzureAdB2COptions
{
    public const string SectionName = "AzureAdB2C";

    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Instance { get; set; } = "https://login.microsoftonline.com/";
    public string SignUpSignInPolicyId { get; set; } = string.Empty;
    public string ResetPasswordPolicyId { get; set; } = string.Empty;
    public string EditProfilePolicyId { get; set; } = string.Empty;
}