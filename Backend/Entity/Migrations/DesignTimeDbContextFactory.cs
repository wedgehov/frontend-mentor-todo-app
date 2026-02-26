using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;
using AppDbContext = Entity.AppDbContext;

namespace Backend.Migrations;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var serverDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Backend/Server"));
        if (!File.Exists(Path.Combine(serverDir, "appsettings.json")))
        {
            serverDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../Server"));
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(serverDir)
            .AddJsonFile("appsettings.json")
            .Build();

        var connStr = configuration.GetConnectionString("DefaultConnection");
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connStr);

        return new AppDbContext(optionsBuilder.Options);
    }
}
