using System.Security.Cryptography;
using DeploymentRunner.Data;
using Microsoft.EntityFrameworkCore;

namespace DeploymentRunner.Services;

public interface IAuthService
{
    Task<string> CreateApiKeyAsync(int userId, string description, DateTime? expiresAt = null, CancellationToken cancellationToken = default);
    Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);
    Task<ApiKey?> GetApiKeyInfoAsync(string apiKey, CancellationToken cancellationToken = default);
    Task<List<ApiKey>> GetUserApiKeysAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> RevokeApiKeyAsync(int keyId, CancellationToken cancellationToken = default);
    Task UpdateLastUsedAsync(string apiKey, CancellationToken cancellationToken = default);
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<AuthService> _logger;

    public AuthService(ApplicationDbContext dbContext, ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<string> CreateApiKeyAsync(int userId, string description, DateTime? expiresAt = null, CancellationToken cancellationToken = default)
    {
        var apiKey = GenerateApiKey();
        var hashedKey = HashApiKey(apiKey);

        var apiKeyEntity = new ApiKey
        {
            Key = hashedKey,
            Description = description,
            UserId = userId,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _dbContext.ApiKeys.Add(apiKeyEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created new API key for user {UserId}: {Description}", userId, description);

        // Return the unhashed key (this is the only time we can return it)
        return apiKey;
    }

    public async Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        var hashedKey = HashApiKey(apiKey);

        var key = await _dbContext.ApiKeys
            .Include(k => k.User)
            .FirstOrDefaultAsync(k => k.Key == hashedKey && k.IsActive, cancellationToken);

        if (key == null)
            return false;

        // Check expiration
        if (key.ExpiresAt.HasValue && key.ExpiresAt.Value < DateTime.UtcNow)
        {
            _logger.LogWarning("API key expired: {KeyId}", key.Id);
            return false;
        }

        return true;
    }

    public async Task<ApiKey?> GetApiKeyInfoAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        var hashedKey = HashApiKey(apiKey);

        return await _dbContext.ApiKeys
            .Include(k => k.User)
            .FirstOrDefaultAsync(k => k.Key == hashedKey && k.IsActive, cancellationToken);
    }

    public async Task<List<ApiKey>> GetUserApiKeysAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ApiKeys
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RevokeApiKeyAsync(int keyId, CancellationToken cancellationToken = default)
    {
        var key = await _dbContext.ApiKeys.FindAsync(new object[] { keyId }, cancellationToken);

        if (key == null)
            return false;

        key.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Revoked API key: {KeyId}", keyId);

        return true;
    }

    public async Task UpdateLastUsedAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        var hashedKey = HashApiKey(apiKey);

        var key = await _dbContext.ApiKeys
            .FirstOrDefaultAsync(k => k.Key == hashedKey, cancellationToken);

        if (key != null)
        {
            key.LastUsedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }

    private string GenerateApiKey()
    {
        // Generate a secure random API key
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        return $"opc_{Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "")}";
    }

    private string HashApiKey(string apiKey)
    {
        using var sha256 = SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(apiKey);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
