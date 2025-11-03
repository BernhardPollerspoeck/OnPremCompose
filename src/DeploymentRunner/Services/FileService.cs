namespace DeploymentRunner.Services;

public interface IFileService
{
    Task SaveComposeFileAsync(string serviceName, string composeContent, Dictionary<string, string>? environment, CancellationToken cancellationToken = default);
    Task<string?> GetComposeFileContentAsync(string serviceName, CancellationToken cancellationToken = default);
    Task<Dictionary<string, string>?> GetEnvironmentVariablesAsync(string serviceName, CancellationToken cancellationToken = default);
    Task BackupComposeFileAsync(string serviceName, CancellationToken cancellationToken = default);
    Task<bool> ServiceDirectoryExistsAsync(string serviceName);
}

public class FileService : IFileService
{
    private readonly string _workingDirectory;
    private readonly string _backupDirectory;
    private readonly ILogger<FileService> _logger;

    public FileService(IConfiguration configuration, ILogger<FileService> logger)
    {
        _workingDirectory = configuration["Docker:WorkingDirectory"] ?? "/app/deployments";
        _backupDirectory = configuration["Backup:Directory"] ?? "/app/backups";
        _logger = logger;

        // Ensure directories exist
        Directory.CreateDirectory(_workingDirectory);
        Directory.CreateDirectory(_backupDirectory);
    }

    public async Task SaveComposeFileAsync(string serviceName, string composeContent, Dictionary<string, string>? environment, CancellationToken cancellationToken = default)
    {
        var servicePath = Path.Combine(_workingDirectory, serviceName);
        Directory.CreateDirectory(servicePath);

        var composePath = Path.Combine(servicePath, "docker-compose.yml");

        // Backup existing file if it exists
        if (File.Exists(composePath))
        {
            await BackupComposeFileAsync(serviceName, cancellationToken);
        }

        // Save compose file
        await File.WriteAllTextAsync(composePath, composeContent, cancellationToken);
        _logger.LogInformation("Saved compose file for service: {ServiceName}", serviceName);

        // Save environment variables if provided
        if (environment != null && environment.Count > 0)
        {
            var envPath = Path.Combine(servicePath, ".env");
            var envContent = string.Join(Environment.NewLine, environment.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            await File.WriteAllTextAsync(envPath, envContent, cancellationToken);
            _logger.LogInformation("Saved environment file for service: {ServiceName}", serviceName);
        }
    }

    public async Task<string?> GetComposeFileContentAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var composePath = Path.Combine(_workingDirectory, serviceName, "docker-compose.yml");

        if (!File.Exists(composePath))
        {
            _logger.LogWarning("Compose file not found for service: {ServiceName}", serviceName);
            return null;
        }

        return await File.ReadAllTextAsync(composePath, cancellationToken);
    }

    public async Task<Dictionary<string, string>?> GetEnvironmentVariablesAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var envPath = Path.Combine(_workingDirectory, serviceName, ".env");

        if (!File.Exists(envPath))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(envPath, cancellationToken);
        var env = new Dictionary<string, string>();

        foreach (var line in content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('=', 2);
            if (parts.Length == 2)
            {
                env[parts[0].Trim()] = parts[1].Trim();
            }
        }

        return env;
    }

    public async Task BackupComposeFileAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var composePath = Path.Combine(_workingDirectory, serviceName, "docker-compose.yml");

        if (!File.Exists(composePath))
        {
            _logger.LogWarning("No compose file to backup for service: {ServiceName}", serviceName);
            return;
        }

        var backupPath = Path.Combine(_backupDirectory, serviceName);
        Directory.CreateDirectory(backupPath);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupFile = Path.Combine(backupPath, $"docker-compose_{timestamp}.yml");

        File.Copy(composePath, backupFile, true);
        _logger.LogInformation("Backed up compose file for service: {ServiceName} to {BackupFile}", serviceName, backupFile);

        // Also backup .env if exists
        var envPath = Path.Combine(_workingDirectory, serviceName, ".env");
        if (File.Exists(envPath))
        {
            var envBackupFile = Path.Combine(backupPath, $".env_{timestamp}");
            File.Copy(envPath, envBackupFile, true);
        }

        // Clean up old backups (keep last 10)
        await CleanupOldBackupsAsync(serviceName, 10, cancellationToken);
    }

    public Task<bool> ServiceDirectoryExistsAsync(string serviceName)
    {
        var servicePath = Path.Combine(_workingDirectory, serviceName);
        return Task.FromResult(Directory.Exists(servicePath));
    }

    private async Task CleanupOldBackupsAsync(string serviceName, int keepCount, CancellationToken cancellationToken)
    {
        var backupPath = Path.Combine(_backupDirectory, serviceName);

        if (!Directory.Exists(backupPath))
            return;

        var files = Directory.GetFiles(backupPath, "docker-compose_*.yml")
                             .OrderByDescending(f => File.GetCreationTime(f))
                             .ToList();

        // Delete old backups
        foreach (var file in files.Skip(keepCount))
        {
            try
            {
                File.Delete(file);
                _logger.LogDebug("Deleted old backup: {File}", file);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old backup: {File}", file);
            }
        }

        await Task.CompletedTask;
    }
}
