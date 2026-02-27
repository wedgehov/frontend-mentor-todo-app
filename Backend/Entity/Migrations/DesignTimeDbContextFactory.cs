using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Linq;
using AppDbContext = Entity.AppDbContext;

namespace Backend.Migrations;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cwd = Directory.GetCurrentDirectory();
        var serverDir =
            new[]
            {
                Path.GetFullPath(Path.Combine(cwd, "Backend/Server")),
                Path.GetFullPath(Path.Combine(cwd, "../Server")),
                Path.GetFullPath(Path.Combine(cwd, "../../Server"))
            }
            .FirstOrDefault(dir => File.Exists(Path.Combine(dir, "appsettings.json")));

        if (serverDir is null)
        {
            throw new DirectoryNotFoundException(
                $"Could not find a Server directory with appsettings.json from '{cwd}'."
            );
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(serverDir)
            .AddJsonFile("appsettings.json")
            .Build();

        var connStr = configuration.GetConnectionString("DefaultConnection");
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connStr, npgsql => npgsql.MigrationsAssembly("Entity"));

        return new AppDbContext(optionsBuilder.Options);
    }
}
