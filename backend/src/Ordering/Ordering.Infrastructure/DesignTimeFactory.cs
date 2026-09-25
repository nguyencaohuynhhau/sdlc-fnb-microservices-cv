using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ordering.Infrastructure;

/// <summary>Chỉ cho <c>dotnet ef migrations add</c> — không mở kết nối, không đọc secret.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<OrderingDbContext>
{
    public OrderingDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<OrderingDbContext>().UseNpgsql("Host=design-time").Options);
}
