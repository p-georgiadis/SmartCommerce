using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using SmartCommerce.NotificationService.Data;
using SmartCommerce.NotificationService.Hubs;
using SmartCommerce.NotificationService.Services;
using SmartCommerce.Shared.Authentication;
using SmartCommerce.Shared.Caching;
using SmartCommerce.Shared.Messaging;
using SmartCommerce.Shared.Telemetry;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.WithProperty("ServiceName", "NotificationService")
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SmartCommerce Notification Service API",
        Version = "v1",
        Description = "Real-time notification and messaging service for SmartCommerce platform"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Database
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("NotificationDb")));

// Redis Cache
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var connectionString = builder.Configuration.GetConnectionString("Redis");
    return ConnectionMultiplexer.Connect(connectionString!);
});

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// SignalR with Redis backplane
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
    options.StreamBufferCapacity = 10;
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
})
.AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis")!, options =>
{
    options.Configuration.ChannelPrefix = "SmartCommerce:SignalR";
});

// Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];

if (string.IsNullOrEmpty(secretKey))
{
    throw new InvalidOperationException("JWT SecretKey is not configured");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };

        // Configure SignalR authentication
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/notificationHub"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// Hangfire for background jobs
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("NotificationDb"), new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));

builder.Services.AddHangfireServer();

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("NotificationApi", limiterOptions =>
    {
        limiterOptions.PermitLimit = 200;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 20;
    });
});

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<NotificationDbContext>("database")
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!, "redis")
    .AddCheck<ServiceBusHealthCheck>("servicebus")
    .AddHangfire(options =>
    {
        options.MinimumAvailableServers = 1;
    });

// Application Services
builder.Services.AddScoped<INotificationService, SmartCommerce.NotificationService.Services.NotificationService>();
builder.Services.AddScoped<INotificationDeliveryService, NotificationDeliveryService>();
builder.Services.AddScoped<IRealTimeNotificationService, RealTimeNotificationService>();

// Mock implementations for external services (replace with real implementations)
builder.Services.AddScoped<IEmailNotificationService, MockEmailNotificationService>();
builder.Services.AddScoped<ISmsNotificationService, MockSmsNotificationService>();
builder.Services.AddScoped<IPushNotificationService, MockPushNotificationService>();

// Shared Services
builder.Services.AddSharedAuthentication(builder.Configuration);
builder.Services.AddSharedCaching(builder.Configuration);
builder.Services.AddSharedMessaging(builder.Configuration);
builder.Services.AddSharedTelemetry(builder.Configuration);

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3001") // Add your frontend URLs
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Required for SignalR
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification Service API v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

// Custom middleware
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapControllers();

// SignalR Hub
app.MapHub<NotificationHub>("/notificationHub");

// Hangfire Dashboard
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

// Health checks endpoint
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                exception = entry.Value.Exception?.Message,
                duration = entry.Value.Duration.ToString()
            })
        };
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
});

// Background jobs
RecurringJob.AddOrUpdate<INotificationService>(
    "process-scheduled-notifications",
    service => service.ProcessScheduledNotificationsAsync(),
    Cron.Minutely);

RecurringJob.AddOrUpdate<INotificationService>(
    "retry-failed-notifications",
    service => service.RetryFailedNotificationsAsync(),
    Cron.Hourly);

RecurringJob.AddOrUpdate<INotificationService>(
    "cleanup-inactive-subscriptions",
    service => service.CleanupInactiveSubscriptionsAsync(),
    Cron.Daily);

// Database migration and seeding
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    await context.Database.MigrateAsync();
}

try
{
    Log.Information("Starting Notification Service");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Notification Service failed to start");
}
finally
{
    Log.CloseAndFlush();
}

// Mock service implementations
public class MockEmailNotificationService : IEmailNotificationService
{
    private readonly ILogger<MockEmailNotificationService> _logger;

    public MockEmailNotificationService(ILogger<MockEmailNotificationService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(string to, string subject, string htmlContent, string? textContent = null)
    {
        _logger.LogInformation("Mock email sent to {To} with subject: {Subject}", to, subject);
        await Task.Delay(100); // Simulate network delay
        return true;
    }

    public async Task<bool> SendBulkEmailAsync(List<string> recipients, string subject, string htmlContent, string? textContent = null)
    {
        _logger.LogInformation("Mock bulk email sent to {Count} recipients with subject: {Subject}", recipients.Count, subject);
        await Task.Delay(200);
        return true;
    }

    public async Task<string> GetDeliveryStatusAsync(string messageId)
    {
        await Task.Delay(50);
        return "delivered";
    }
}

public class MockSmsNotificationService : ISmsNotificationService
{
    private readonly ILogger<MockSmsNotificationService> _logger;

    public MockSmsNotificationService(ILogger<MockSmsNotificationService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> SendSmsAsync(string to, string message)
    {
        _logger.LogInformation("Mock SMS sent to {To}: {Message}", to, message);
        await Task.Delay(100);
        return true;
    }

    public async Task<bool> SendBulkSmsAsync(List<string> recipients, string message)
    {
        _logger.LogInformation("Mock bulk SMS sent to {Count} recipients: {Message}", recipients.Count, message);
        await Task.Delay(200);
        return true;
    }

    public async Task<string> GetDeliveryStatusAsync(string messageId)
    {
        await Task.Delay(50);
        return "delivered";
    }
}

public class MockPushNotificationService : IPushNotificationService
{
    private readonly ILogger<MockPushNotificationService> _logger;

    public MockPushNotificationService(ILogger<MockPushNotificationService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> SendPushNotificationAsync(Guid userId, string title, string message, Dictionary<string, object>? data = null)
    {
        _logger.LogInformation("Mock push notification sent to user {UserId}: {Title} - {Message}", userId, title, message);
        await Task.Delay(100);
        return true;
    }

    public async Task<bool> SendPushNotificationToSubscriptionAsync(NotificationSubscriptionDto subscription, string title, string message, Dictionary<string, object>? data = null)
    {
        _logger.LogInformation("Mock push notification sent to subscription {SubscriptionId}: {Title} - {Message}", subscription.Id, title, message);
        await Task.Delay(100);
        return true;
    }

    public async Task<bool> SendBulkPushNotificationAsync(List<Guid> userIds, string title, string message, Dictionary<string, object>? data = null)
    {
        _logger.LogInformation("Mock bulk push notification sent to {Count} users: {Title} - {Message}", userIds.Count, title, message);
        await Task.Delay(200);
        return true;
    }

    public async Task<bool> ValidateSubscriptionAsync(NotificationSubscriptionDto subscription)
    {
        await Task.Delay(50);
        return true;
    }
}

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.IsInRole("Admin") || httpContext.Request.IsLocal();
    }
}