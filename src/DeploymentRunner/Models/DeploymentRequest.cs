namespace DeploymentRunner.Models;

public class DeploymentRequest
{
    public required string ServiceName { get; set; }
    public required string ComposeFile { get; set; }
    public Dictionary<string, string>? Environment { get; set; }
    public bool PullImages { get; set; } = true;
}

public class DeploymentResponse
{
    public required string ServiceName { get; set; }
    public required string Status { get; set; }
    public string? Message { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
