using DeploymentRunner.Components;
using DeploymentRunner.Data;
using DeploymentRunner.Models;
using DeploymentRunner.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/deployment-runner-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=deploymentrunner.db"));

// Services
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IDockerService, DockerService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Authentication
builder.Services.AddAuthentication("ApiKey")
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", null);

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();

    // Create default admin user if no users exist
    if (!db.Users.Any())
    {
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var defaultUser = new User
        {
            Username = "admin",
            Email = "admin@localhost",
            PasswordHash = authService.HashPassword("admin"),
            IsAdmin = true
        };
        db.Users.Add(defaultUser);
        await db.SaveChangesAsync();

        // Create default API key
        var apiKey = await authService.CreateApiKeyAsync(defaultUser.Id, "Default API Key");
        Log.Information("Created default admin user. Username: admin, Password: admin");
        Log.Information("Created default API key: {ApiKey}", apiKey);
    }
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Map Blazor components
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// ============================================================================
// API Endpoints
// ============================================================================

// Health check
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithOpenApi();

// Deploy service
app.MapPost("/api/deploy", async (DeploymentRequest request, IDockerService dockerService) =>
{
    var result = await dockerService.DeployServiceAsync(request);
    return result.Status == "success" ? Results.Ok(result) : Results.BadRequest(result);
})
    .RequireAuthorization()
    .WithName("DeployService")
    .WithOpenApi();

// Get all services
app.MapGet("/api/services", async (IDockerService dockerService) =>
{
    var services = await dockerService.GetAllServicesStatusAsync();
    return Results.Ok(services);
})
    .RequireAuthorization()
    .WithName("GetAllServices")
    .WithOpenApi();

// Get service details
app.MapGet("/api/services/{name}", async (string name, IDockerService dockerService) =>
{
    var status = await dockerService.GetServiceStatusAsync(name);
    return status != null ? Results.Ok(status) : Results.NotFound();
})
    .RequireAuthorization()
    .WithName("GetServiceDetails")
    .WithOpenApi();

// Get service logs
app.MapGet("/api/services/{name}/logs", async (string name, int lines, IDockerService dockerService) =>
{
    var logs = await dockerService.GetServiceLogsAsync(name, lines);
    return Results.Ok(new { serviceName = name, logs });
})
    .RequireAuthorization()
    .WithName("GetServiceLogs")
    .WithOpenApi();

// Restart service
app.MapPost("/api/services/{name}/restart", async (string name, IDockerService dockerService) =>
{
    var result = await dockerService.RestartServiceAsync(name);
    return result.Status == "success" ? Results.Ok(result) : Results.BadRequest(result);
})
    .RequireAuthorization()
    .WithName("RestartService")
    .WithOpenApi();

// Stop service
app.MapPost("/api/services/{name}/stop", async (string name, IDockerService dockerService) =>
{
    var result = await dockerService.StopServiceAsync(name);
    return result.Status == "success" ? Results.Ok(result) : Results.BadRequest(result);
})
    .RequireAuthorization()
    .WithName("StopService")
    .WithOpenApi();

// Delete service
app.MapDelete("/api/services/{name}", async (string name, IDockerService dockerService) =>
{
    var result = await dockerService.DeleteServiceAsync(name);
    return result.Status == "success" ? Results.Ok(result) : Results.BadRequest(result);
})
    .RequireAuthorization()
    .WithName("DeleteService")
    .WithOpenApi();

// Container-level operations
app.MapPost("/api/services/{serviceName}/containers/{containerName}/start",
    async (string serviceName, string containerName, IDockerService dockerService) =>
{
    var result = await dockerService.StartContainerAsync(serviceName, containerName);
    return result.Status == "success" ? Results.Ok(result) : Results.BadRequest(result);
})
    .RequireAuthorization()
    .WithName("StartContainer")
    .WithOpenApi();

app.MapPost("/api/services/{serviceName}/containers/{containerName}/stop",
    async (string serviceName, string containerName, IDockerService dockerService) =>
{
    var result = await dockerService.StopContainerAsync(serviceName, containerName);
    return result.Status == "success" ? Results.Ok(result) : Results.BadRequest(result);
})
    .RequireAuthorization()
    .WithName("StopContainer")
    .WithOpenApi();

app.MapPost("/api/services/{serviceName}/containers/{containerName}/restart",
    async (string serviceName, string containerName, IDockerService dockerService) =>
{
    var result = await dockerService.RestartContainerAsync(serviceName, containerName);
    return result.Status == "success" ? Results.Ok(result) : Results.BadRequest(result);
})
    .RequireAuthorization()
    .WithName("RestartContainer")
    .WithOpenApi();

app.MapGet("/api/services/{serviceName}/containers/{containerName}/logs",
    async (string serviceName, string containerName, int lines, IDockerService dockerService) =>
{
    var logs = await dockerService.GetContainerLogsAsync(serviceName, containerName, lines);
    return Results.Ok(new { containerName, logs });
})
    .RequireAuthorization()
    .WithName("GetContainerLogs")
    .WithOpenApi();

// API Key management endpoints
app.MapGet("/api/keys", async (HttpContext context, IAuthService authService, ApplicationDbContext db) =>
{
    var apiKeyHeader = context.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
    if (string.IsNullOrEmpty(apiKeyHeader))
        return Results.Unauthorized();

    var keyInfo = await authService.GetApiKeyInfoAsync(apiKeyHeader);
    if (keyInfo?.User == null)
        return Results.Unauthorized();

    var keys = await authService.GetUserApiKeysAsync(keyInfo.UserId);
    return Results.Ok(keys.Select(k => new
    {
        k.Id,
        k.Description,
        k.CreatedAt,
        k.ExpiresAt,
        k.LastUsedAt,
        k.IsActive,
        Key = "***" // Don't expose actual keys
    }));
})
    .RequireAuthorization()
    .WithName("GetApiKeys")
    .WithOpenApi();

app.MapPost("/api/keys", async (HttpContext context, CreateApiKeyRequest request, IAuthService authService) =>
{
    var apiKeyHeader = context.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
    if (string.IsNullOrEmpty(apiKeyHeader))
        return Results.Unauthorized();

    var keyInfo = await authService.GetApiKeyInfoAsync(apiKeyHeader);
    if (keyInfo?.User == null)
        return Results.Unauthorized();

    var newKey = await authService.CreateApiKeyAsync(keyInfo.UserId, request.Description, request.ExpiresAt);
    return Results.Ok(new { apiKey = newKey, message = "Save this key securely. It won't be shown again." });
})
    .RequireAuthorization()
    .WithName("CreateApiKey")
    .WithOpenApi();

app.MapDelete("/api/keys/{id}", async (int id, HttpContext context, IAuthService authService) =>
{
    var apiKeyHeader = context.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
    if (string.IsNullOrEmpty(apiKeyHeader))
        return Results.Unauthorized();

    var result = await authService.RevokeApiKeyAsync(id);
    return result ? Results.Ok(new { message = "API key revoked" }) : Results.NotFound();
})
    .RequireAuthorization()
    .WithName("RevokeApiKey")
    .WithOpenApi();

Log.Information("Starting DeploymentRunner...");
app.Run();

// Request models
record CreateApiKeyRequest(string Description, DateTime? ExpiresAt);
