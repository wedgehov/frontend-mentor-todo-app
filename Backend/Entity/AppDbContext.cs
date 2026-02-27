using Microsoft.EntityFrameworkCore;

namespace Entity;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public virtual DbSet<User> Users { get; set; } = null!;
    public virtual DbSet<Todo> Todos { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Todo>()
            .HasIndex(t => new { t.UserId, t.Position })
            .IsUnique()
            .HasDatabaseName("IX_todos_user_id_position_unique");
    }
}
