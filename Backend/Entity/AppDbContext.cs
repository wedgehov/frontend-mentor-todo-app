using Microsoft.EntityFrameworkCore;

namespace Entity;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public virtual DbSet<User> Users { get; set; } = null!;
    public virtual DbSet<Todo> Todos { get; set; } = null!;
}
