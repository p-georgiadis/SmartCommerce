using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace SmartCommerce.Shared.Authentication;

public static class JwtConfiguration
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Jwt");
        var azureAdSettings = configuration.GetSection("AzureAd");

        // Check if using Azure AD or custom JWT
        if (!string.IsNullOrEmpty(azureAdSettings["Authority"]))
        {
            // Azure AD configuration
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = azureAdSettings["Authority"];
                    options.Audience = azureAdSettings["Audience"];
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = azureAdSettings["Issuer"],
                        ValidateAudience = true,
                        ValidAudience = azureAdSettings["Audience"],
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(5),
                        RoleClaimType = "roles",
                        NameClaimType = "name"
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                            return Task.CompletedTask;
                        },
                        OnChallenge = context =>
                        {
                            Console.WriteLine($"JWT Challenge: {context.Error} - {context.ErrorDescription}");
                            return Task.CompletedTask;
                        }
                    };
                });
        }
        else
        {
            // Custom JWT configuration
            var key = Encoding.ASCII.GetBytes(jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Key is required"));

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = true,
                        ValidIssuer = jwtSettings["Issuer"],
                        ValidateAudience = true,
                        ValidAudience = jwtSettings["Audience"],
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(5)
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                            return Task.CompletedTask;
                        },
                        OnChallenge = context =>
                        {
                            Console.WriteLine($"JWT Challenge: {context.Error} - {context.ErrorDescription}");
                            return Task.CompletedTask;
                        }
                    };
                });
        }

        return services;
    }

    public static IServiceCollection AddCustomAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // Default policies
            options.AddPolicy("AdminOnly", policy =>
                policy.RequireRole("Admin"));

            options.AddPolicy("ManagerOrAdmin", policy =>
                policy.RequireRole("Admin", "Manager"));

            options.AddPolicy("CustomerOrAdmin", policy =>
                policy.RequireRole("Admin", "Customer"));

            options.AddPolicy("FulfillmentTeam", policy =>
                policy.RequireRole("Admin", "Manager", "Fulfillment"));

            // Scope-based policies for Azure AD
            options.AddPolicy("ReadAccess", policy =>
                policy.RequireClaim("scope", "read"));

            options.AddPolicy("WriteAccess", policy =>
                policy.RequireClaim("scope", "write"));

            options.AddPolicy("FullAccess", policy =>
                policy.RequireClaim("scope", "read", "write"));
        });

        return services;
    }
}