using System.ComponentModel.DataAnnotations;

namespace DeploymentRunner.Data;

public class User
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public required string Username { get; set; }

    [Required]
    public required string PasswordHash { get; set; }

    [Required]
    [EmailAddress]
    public required string Email { get; set; }

    public bool IsAdmin { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
}

public class ApiKey
{
    public int Id { get; set; }

    [Required]
    public required string Key { get; set; }

    [Required]
    [MaxLength(200)]
    public required string Description { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }

    public bool IsActive { get; set; } = true;
}

public class DeploymentHistory
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public required string ServiceName { get; set; }

    [Required]
    public required string Action { get; set; } // deploy, restart, stop, delete

    [Required]
    public required string Status { get; set; } // success, failed, in_progress

    public string? ComposeFileContent { get; set; }
    public string? ErrorMessage { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public class ManagedService
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public required string Name { get; set; }

    [Required]
    public required string ComposeFilePath { get; set; }

    public string? EnvironmentVariables { get; set; } // JSON

    [Required]
    public required string Status { get; set; } // running, stopped, failed

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    public ICollection<DeploymentHistory> Deployments { get; set; } = new List<DeploymentHistory>();
}
