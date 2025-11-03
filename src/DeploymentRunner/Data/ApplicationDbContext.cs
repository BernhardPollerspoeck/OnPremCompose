using Microsoft.EntityFrameworkCore;

namespace DeploymentRunner.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<DeploymentHistory> DeploymentHistory => Set<DeploymentHistory>();
    public DbSet<ManagedService> ManagedServices => Set<ManagedService>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasMany(e => e.ApiKeys)
                  .WithOne(e => e.User)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ApiKey configuration
        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
        });

        // DeploymentHistory configuration
        modelBuilder.Entity<DeploymentHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ServiceName);
            entity.HasIndex(e => e.StartedAt);
        });

        // ManagedService configuration
        modelBuilder.Entity<ManagedService>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasMany(e => e.Deployments)
                  .WithOne()
                  .HasForeignKey("ServiceName")
                  .HasPrincipalKey(e => e.Name)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
