using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Identity.Infrastructure;

/// <summary>Chỉ cho <c>dotnet ef migrations add</c> — không mở kết nối, không đọc secret.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql("Host=design-time").Options);
}
