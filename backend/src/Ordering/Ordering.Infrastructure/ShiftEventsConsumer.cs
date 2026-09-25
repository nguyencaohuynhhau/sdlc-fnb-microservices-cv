using Microsoft.EntityFrameworkCore;
using Ordering.Application;
using Shared.Kernel;
using Shared.Messaging;

namespace Ordering.Infrastructure;

// Bản sao hợp đồng sự kiện của cashier: cùng topic, cùng tên trường JSON. Ordering không tham chiếu
// Cashier.Domain — hai dịch vụ chỉ chung nhau hình dạng message.
[Topic(Topics.ShiftOpened)]
public sealed record ShiftOpened(Guid ShiftId, DateTimeOffset OpenedAt) : IntegrationEvent
{
    public override string PartitionKey => ShiftId.ToString();
}

[Topic(Topics.ShiftClosed)]
public sealed record ShiftClosed(Guid ShiftId, DateTimeOffset ClosedAt) : IntegrationEvent
{
    public override string PartitionKey => ShiftId.ToString();
}

/// <summary>
/// Một ca mà ordering đã nghe tới. Mở và đóng ca đi trên HAI topic khác nhau nên Kafka không giữ
/// thứ tự giữa chúng: "đóng ca A" có thể tới trước "mở ca A". Lưu cả hai mốc cho từng ca thay vì
/// một dòng "ca hiện hành" thì kết quả đúng bất kể thứ tự tới.
/// </summary>
public sealed class KnownShift
{
    public Guid ShiftId { get; set; }

    public DateTimeOffset? OpenedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }
}

public sealed class ShiftEventsConsumer(OrderingDbContext db) :
    IIntegrationEventHandler<ShiftOpened>,
    IIntegrationEventHandler<ShiftClosed>
{
    public async Task HandleAsync(ShiftOpened e, CancellationToken ct) => (await GetOrAddAsync(e.ShiftId, ct)).OpenedAt = e.OpenedAt;

    public async Task HandleAsync(ShiftClosed e, CancellationToken ct) => (await GetOrAddAsync(e.ShiftId, ct)).ClosedAt = e.ClosedAt;

    private async Task<KnownShift> GetOrAddAsync(Guid shiftId, CancellationToken ct)
    {
        var shift = await db.KnownShifts.FindAsync([shiftId], ct);
        if (shift is null)
        {
            shift = new KnownShift { ShiftId = shiftId };
            db.KnownShifts.Add(shift);
        }

        return shift;
    }
}

public sealed class CurrentShiftReader(OrderingDbContext db) : ICurrentShift
{
    /// <summary>Ca đã mở, chưa đóng, mở gần nhất. Cashier đảm bảo chỉ có một; lấy mới nhất để chịu được sự kiện tới muộn.</summary>
    public Task<CurrentShiftView?> GetAsync(CancellationToken ct) =>
        db.KnownShifts.AsNoTracking()
            .Where(s => s.OpenedAt != null && s.ClosedAt == null)
            .OrderByDescending(s => s.OpenedAt)
            .Select(s => new CurrentShiftView(s.ShiftId, s.OpenedAt!.Value))
            .FirstOrDefaultAsync(ct);
}
