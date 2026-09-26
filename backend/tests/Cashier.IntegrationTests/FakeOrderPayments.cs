using System.Collections.Concurrent;
using Cashier.Application;
using Shared.Kernel;

namespace Cashier.IntegrationTests;

/// <summary>
/// Ordering giả, cư xử như <c>Order.MarkPaid</c> thật: nhớ đơn nào đã trả bằng payment nào. Ordering
/// thật qua gRPC có test riêng (<c>MarkPaidTests</c>) và e2e — ở đây chỉ soi phía cashier.
/// </summary>
public sealed class FakeOrderPayments : IOrderPayments
{
    private readonly ConcurrentDictionary<Guid, Guid> _paid = new();
    private int _calls;

    /// <summary>Tổng thật của đơn; đơn không có ở đây coi như tổng khớp.</summary>
    public ConcurrentDictionary<Guid, decimal> Totals { get; } = new();

    /// <summary>Thời gian "mạng + ordering" mỗi lần gọi — để dựng tình huống song song.</summary>
    public TimeSpan Delay { get; set; }

    /// <summary>Lần gọi kế tiếp: ordering đã xử lý (<c>true</c>) hoặc chưa (<c>false</c>), rồi phản hồi mất → 503.</summary>
    public bool? LoseNextReply { get; set; }

    public int Calls => _calls;

    public void Reset()
    {
        Totals.Clear();
        Delay = TimeSpan.Zero;
        LoseNextReply = null;
        _calls = 0;
    }

    public async Task<MarkPaidResult> MarkPaidAsync(Guid orderId, decimal expectedTotal, Guid paymentId, CancellationToken ct)
    {
        Interlocked.Increment(ref _calls);
        await Task.Delay(Delay, ct);

        MarkPaidResult result;
        if (_paid.TryGetValue(orderId, out var existing))
        {
            result = new(existing == paymentId ? MarkPaidStatus.Ok : MarkPaidStatus.AlreadyPaid);
        }
        else if (Totals.TryGetValue(orderId, out var total) && total != expectedTotal)
        {
            result = new(MarkPaidStatus.TotalMismatch, total);
        }
        else
        {
            result = new(_paid.GetOrAdd(orderId, paymentId) == paymentId ? MarkPaidStatus.Ok : MarkPaidStatus.AlreadyPaid);
        }

        if (LoseNextReply is { } processed)
        {
            LoseNextReply = null;
            if (!processed)
            {
                _paid.TryRemove(new KeyValuePair<Guid, Guid>(orderId, paymentId));
            }

            throw new DependencyUnavailableException(PayOrderHandler.OrderingUnavailableMessage);
        }

        return result;
    }
}
