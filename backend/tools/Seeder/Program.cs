using Npgsql;
using Seeder;

// Chạy từ máy dev: đọc .env ở gốc repo (nếu có) — biến môi trường đã đặt thì thắng.
foreach (var path in new[] { ".env", "../.env" }.Where(File.Exists))
{
    foreach (var line in File.ReadAllLines(path))
    {
        var eq = line.IndexOf('=');
        if (line.TrimStart().StartsWith('#') || eq <= 0)
        {
            continue;
        }

        var key = line[..eq].Trim();
        Environment.SetEnvironmentVariable(key, Environment.GetEnvironmentVariable(key) ?? line[(eq + 1)..].Trim());
    }
}

var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
if (!DemoData.IsAllowed(env))
{
    Console.Error.WriteLine($"Từ chối seed: ASPNETCORE_ENVIRONMENT={env ?? "(trống)"}. Seeder chỉ chạy khi {DemoData.AllowedEnvironment}.");
    return 1;
}

var pgPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
var seedPassword = Environment.GetEnvironmentVariable("SEED_PASSWORD");
if (string.IsNullOrEmpty(pgPassword) || seedPassword is not { Length: >= 8 })
{
    Console.Error.WriteLine("Cần POSTGRES_PASSWORD và SEED_PASSWORD (tối thiểu 8 ký tự) trong .env hoặc biến môi trường.");
    return 1;
}

// Postgres của compose publish 127.0.0.1:54329 chỉ cho máy dev.
string Conn(string db) => new NpgsqlConnectionStringBuilder
{
    Host = "localhost",
    Port = 54329,
    Username = "postgres",
    Password = pgPassword,
    Database = db,
}.ConnectionString;

Console.WriteLine(await DemoData.SeedAsync(Conn, seedPassword, DateTimeOffset.UtcNow));
return 0;
