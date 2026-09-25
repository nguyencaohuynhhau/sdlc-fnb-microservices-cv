using Identity.Application;
using StackExchange.Redis;

namespace Identity.Infrastructure;

/// <summary>
/// Đếm lần sai trong Redis, key <c>login-fail:{username}</c>, hết hạn 15 phút kể từ lần sai ĐẦU TIÊN.
/// Để ở Redis (không ở DB) vì đây là bộ đếm tạm, và nhiều bản identity cùng thấy một con số.
/// </summary>
public sealed class LoginAttemptStore(IConnectionMultiplexer redis) : ILoginAttemptStore
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private static RedisKey Key(string username) => $"login-fail:{username}";

    public async Task<long> FailureCountAsync(string username) =>
        (long?)await redis.GetDatabase().StringGetAsync(Key(username)) ?? 0;

    public async Task RecordFailureAsync(string username)
    {
        var db = redis.GetDatabase();
        await db.StringIncrementAsync(Key(username));
        await db.KeyExpireAsync(Key(username), Window, ExpireWhen.HasNoExpiry);
    }

    public Task ResetAsync(string username) => redis.GetDatabase().KeyDeleteAsync(Key(username));
}
