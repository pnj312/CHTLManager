using Microsoft.EntityFrameworkCore.Design;

namespace CHTLManager.DAL;

/// <summary>Cho công cụ "dotnet ef" tạo AppDbContext khi sinh migration.</summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => new();
}
