using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cashier.Infrastructure;

/// <summary>Chỉ cho <c>dotnet ef migrations add</c> — không mở kết nối, không đọc secret.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<CashierDbContext>
{
    public CashierDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CashierDbContext>().UseNpgsql("Host=design-time").Options);
}
