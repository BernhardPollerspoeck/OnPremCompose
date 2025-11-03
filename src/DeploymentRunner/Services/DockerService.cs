using System.Diagnostics;
using System.Text;
using DeploymentRunner.Data;
using DeploymentRunner.Models;
using Microsoft.EntityFrameworkCore;

namespace DeploymentRunner.Services;

public interface IDockerService
{
    Task<DeploymentResponse> DeployServiceAsync(DeploymentRequest request, CancellationToken cancellationToken = default);
    Task<DeploymentResponse> StopServiceAsync(string serviceName, CancellationToken cancellationToken = default);
    Task<DeploymentResponse> RestartServiceAsync(string serviceName, CancellationToken cancellationToken = default);
    Task<DeploymentResponse> DeleteServiceAsync(string serviceName, CancellationToken cancellationToken = default);
    Task<ServiceStatus?> GetServiceStatusAsync(string serviceName, CancellationToken cancellationToken = default);
    Task<List<ServiceStatus>> GetAllServicesStatusAsync(CancellationToken cancellationToken = default);
    Task<string> GetServiceLogsAsync(string serviceName, int lines = 100, CancellationToken cancellationToken = default);
    Task<string> GetContainerLogsAsync(string serviceName, string containerName, int lines = 100, CancellationToken cancellationToken = default);
    Task<DeploymentResponse> StartContainerAsync(string serviceName, string containerName, CancellationToken cancellationToken = default);
    Task<DeploymentResponse> StopContainerAsync(string serviceName, string containerName, CancellationToken cancellationToken = default);
    Task<DeploymentResponse> RestartContainerAsync(string serviceName, string containerName, CancellationToken cancellationToken = default);
}

public class DockerService : IDockerService
{
    private readonly IFileService _fileService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DockerService> _logger;
    private readonly string _workingDirectory;

    public DockerService(
        IFileService fileService,
        ApplicationDbContext dbContext,
        ILogger<DockerService> logger,
        IConfiguration configuration)
    {
        _fileService = fileService;
        _dbContext = dbContext;
        _logger = logger;
        _workingDirectory = configuration["Docker:WorkingDirectory"] ?? "/app/deployments";

        // Ensure working directory exists
        Directory.CreateDirectory(_workingDirectory);
    }

    public async Task<DeploymentResponse> DeployServiceAsync(DeploymentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deploying service: {ServiceName}", request.ServiceName);

        var history = new DeploymentHistory
        {
            ServiceName = request.ServiceName,
            Action = "deploy",
            Status = "in_progress",
            ComposeFileContent = request.ComposeFile
        };
        _dbContext.DeploymentHistory.Add(history);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            // Save compose file
            var servicePath = Path.Combine(_workingDirectory, request.ServiceName);
            await _fileService.SaveComposeFileAsync(request.ServiceName, request.ComposeFile, request.Environment, cancellationToken);

            // Pull images if requested
            if (request.PullImages)
            {
                _logger.LogInformation("Pulling images for service: {ServiceName}", request.ServiceName);
                await ExecuteDockerComposeCommandAsync(servicePath, "pull", cancellationToken);
            }

            // Deploy (up -d)
            _logger.LogInformation("Starting containers for service: {ServiceName}", request.ServiceName);
            var output = await ExecuteDockerComposeCommandAsync(servicePath, "up -d --remove-orphans", cancellationToken);

            // Update or create managed service
            var managedService = await _dbContext.ManagedServices
                .FirstOrDefaultAsync(s => s.Name == request.ServiceName, cancellationToken);

            if (managedService == null)
            {
                managedService = new ManagedService
                {
                    Name = request.ServiceName,
                    ComposeFilePath = Path.Combine(servicePath, "docker-compose.yml"),
                    EnvironmentVariables = System.Text.Json.JsonSerializer.Serialize(request.Environment ?? new Dictionary<string, string>()),
                    Status = "running"
                };
                _dbContext.ManagedServices.Add(managedService);
            }
            else
            {
                managedService.Status = "running";
                managedService.LastUpdated = DateTime.UtcNow;
                managedService.EnvironmentVariables = System.Text.Json.JsonSerializer.Serialize(request.Environment ?? new Dictionary<string, string>());
            }

            history.Status = "success";
            history.CompletedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully deployed service: {ServiceName}", request.ServiceName);

            return new DeploymentResponse
            {
                ServiceName = request.ServiceName,
                Status = "success",
                Message = $"Service deployed successfully\n{output}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deploy service: {ServiceName}", request.ServiceName);

            history.Status = "failed";
            history.ErrorMessage = ex.Message;
            history.CompletedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new DeploymentResponse
            {
                ServiceName = request.ServiceName,
                Status = "failed",
                Message = $"Deployment failed: {ex.Message}"
            };
        }
    }

    public async Task<DeploymentResponse> StopServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping service: {ServiceName}", serviceName);

        try
        {
            var servicePath = Path.Combine(_workingDirectory, serviceName);
            var output = await ExecuteDockerComposeCommandAsync(servicePath, "stop", cancellationToken);

            var managedService = await _dbContext.ManagedServices
                .FirstOrDefaultAsync(s => s.Name == serviceName, cancellationToken);

            if (managedService != null)
            {
                managedService.Status = "stopped";
                managedService.LastUpdated = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return new DeploymentResponse
            {
                ServiceName = serviceName,
                Status = "success",
                Message = $"Service stopped successfully\n{output}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop service: {ServiceName}", serviceName);
            return new DeploymentResponse
            {
                ServiceName = serviceName,
                Status = "failed",
                Message = $"Stop failed: {ex.Message}"
            };
        }
    }

    public async Task<DeploymentResponse> RestartServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Restarting service: {ServiceName}", serviceName);

        try
        {
            var servicePath = Path.Combine(_workingDirectory, serviceName);
            var output = await ExecuteDockerComposeCommandAsync(servicePath, "restart", cancellationToken);

            var managedService = await _dbContext.ManagedServices
                .FirstOrDefaultAsync(s => s.Name == serviceName, cancellationToken);

            if (managedService != null)
            {
                managedService.Status = "running";
                managedService.LastUpdated = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return new DeploymentResponse
            {
                ServiceName = serviceName,
                Status = "success",
                Message = $"Service restarted successfully\n{output}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart service: {ServiceName}", serviceName);
            return new DeploymentResponse
            {
                ServiceName = serviceName,
                Status = "failed",
                Message = $"Restart failed: {ex.Message}"
            };
        }
    }

    public async Task<DeploymentResponse> DeleteServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting service: {ServiceName}", serviceName);

        try
        {
            var servicePath = Path.Combine(_workingDirectory, serviceName);
            var output = await ExecuteDockerComposeCommandAsync(servicePath, "down -v", cancellationToken);

            var managedService = await _dbContext.ManagedServices
                .FirstOrDefaultAsync(s => s.Name == serviceName, cancellationToken);

            if (managedService != null)
            {
                _dbContext.ManagedServices.Remove(managedService);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // Clean up files
            if (Directory.Exists(servicePath))
            {
                Directory.Delete(servicePath, true);
            }

            return new DeploymentResponse
            {
                ServiceName = serviceName,
                Status = "success",
                Message = $"Service deleted successfully\n{output}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete service: {ServiceName}", serviceName);
            return new DeploymentResponse
            {
                ServiceName = serviceName,
                Status = "failed",
                Message = $"Delete failed: {ex.Message}"
            };
        }
    }

    public async Task<ServiceStatus?> GetServiceStatusAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        try
        {
            var servicePath = Path.Combine(_workingDirectory, serviceName);
            var output = await ExecuteDockerComposeCommandAsync(servicePath, "ps --format json", cancellationToken);

            var containers = ParseContainersFromJson(output);
            var status = DetermineOverallStatus(containers);

            return new ServiceStatus
            {
                Name = serviceName,
                Status = status,
                Containers = containers,
                ComposeFile = await _fileService.GetComposeFileContentAsync(serviceName, cancellationToken)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get status for service: {ServiceName}", serviceName);
            return null;
        }
    }

    public async Task<List<ServiceStatus>> GetAllServicesStatusAsync(CancellationToken cancellationToken = default)
    {
        var services = await _dbContext.ManagedServices.ToListAsync(cancellationToken);
        var statusList = new List<ServiceStatus>();

        foreach (var service in services)
        {
            var status = await GetServiceStatusAsync(service.Name, cancellationToken);
            if (status != null)
            {
                statusList.Add(status);
            }
        }

        return statusList;
    }

    public async Task<string> GetServiceLogsAsync(string serviceName, int lines = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            var servicePath = Path.Combine(_workingDirectory, serviceName);
            return await ExecuteDockerComposeCommandAsync(servicePath, $"logs --tail={lines}", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get logs for service: {ServiceName}", serviceName);
            return $"Error retrieving logs: {ex.Message}";
        }
    }

    public async Task<string> GetContainerLogsAsync(string serviceName, string containerName, int lines = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            var output = await ExecuteDockerCommandAsync($"logs --tail={lines} {containerName}", cancellationToken);
            return output;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get logs for container: {ContainerName}", containerName);
            return $"Error retrieving logs: {ex.Message}";
        }
    }

    public async Task<DeploymentResponse> StartContainerAsync(string serviceName, string containerName, CancellationToken cancellationToken = default)
    {
        try
        {
            var output = await ExecuteDockerCommandAsync($"start {containerName}", cancellationToken);
            return new DeploymentResponse
            {
                ServiceName = serviceName,
                Status = "success",
                Message = $"Container {containerName} started successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start container: {ContainerName}", containerName);
            return new DeploymentResponse
            {
                ServiceName = serviceName,
                Status = "failed",
                Message = $"Failed to start container: {ex.Message}"
            };
        }
    }

    public async Task<DeploymentResponse> StopContainerAsync(string serviceName, string containerName, CancellationToken cancellationToken = default)
    {
        try
        {
            var output = await ExecuteDockerCommandAsync($"stop {containerName}", cancellationToken);
            return new DeploymentResponse
            {
                ServiceName = serviceName,
                Status = "success",
                Message = $"Container {containerName} stopped successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop container: {ContainerName}", containerName);
            return new DeploymentResponse
            {
                ServiceName = serviceName,
                Status = "failed",
                Message = $"Failed to stop container: {ex.Message}"
            };
        }
    }

    public async Task<DeploymentResponse> RestartContainerAsync(string serviceName, string containerName, CancellationToken cancellationToken = default)
    {
        try
        {
            var output = await ExecuteDockerCommandAsync($"restart {containerName}", cancellationToken);
            return new DeploymentResponse
            {
                ServiceName = serviceName,
                Status = "success",
                Message = $"Container {containerName} restarted successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart container: {ContainerName}", containerName);
            return new DeploymentResponse
            {
                ServiceName = serviceName,
                Status = "failed",
                Message = $"Failed to restart container: {ex.Message}"
            };
        }
    }

    private async Task<string> ExecuteDockerComposeCommandAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"compose {arguments}",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (sender, args) =>
        {
            if (args.Data != null) output.AppendLine(args.Data);
        };

        process.ErrorDataReceived += (sender, args) =>
        {
            if (args.Data != null) error.AppendLine(args.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new Exception($"Docker compose command failed: {error}");
        }

        return output.ToString();
    }

    private async Task<string> ExecuteDockerCommandAsync(string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (sender, args) =>
        {
            if (args.Data != null) output.AppendLine(args.Data);
        };

        process.ErrorDataReceived += (sender, args) =>
        {
            if (args.Data != null) error.AppendLine(args.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new Exception($"Docker command failed: {error}");
        }

        return output.ToString();
    }

    private List<ContainerInfo> ParseContainersFromJson(string jsonOutput)
    {
        var containers = new List<ContainerInfo>();

        if (string.IsNullOrWhiteSpace(jsonOutput))
            return containers;

        try
        {
            var lines = jsonOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var doc = System.Text.Json.JsonDocument.Parse(line);
                var root = doc.RootElement;

                containers.Add(new ContainerInfo
                {
                    Id = root.GetProperty("ID").GetString() ?? "",
                    Name = root.GetProperty("Name").GetString() ?? "",
                    Status = root.GetProperty("State").GetString() ?? "",
                    Image = root.GetProperty("Image").GetString() ?? "",
                    Created = DateTime.TryParse(root.GetProperty("CreatedAt").GetString(), out var created) ? created : DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse container JSON output");
        }

        return containers;
    }

    private string DetermineOverallStatus(List<ContainerInfo> containers)
    {
        if (containers.Count == 0) return "stopped";
        if (containers.All(c => c.Status.Contains("running", StringComparison.OrdinalIgnoreCase))) return "running";
        if (containers.Any(c => c.Status.Contains("running", StringComparison.OrdinalIgnoreCase))) return "partial";
        return "stopped";
    }
}
