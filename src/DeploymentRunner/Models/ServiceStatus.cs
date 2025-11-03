namespace DeploymentRunner.Models;

public class ServiceStatus
{
    public required string Name { get; set; }
    public required string Status { get; set; } // running, stopped, failed
    public List<ContainerInfo> Containers { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public string? ComposeFile { get; set; }
}

public class ContainerInfo
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Status { get; set; }
    public required string Image { get; set; }
    public DateTime Created { get; set; }
}
