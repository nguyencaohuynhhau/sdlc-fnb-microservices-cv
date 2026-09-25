using Shared.Kernel;

namespace Cashier.Domain;

/// <summary>
/// Ca làm việc. Mọi đơn hàng thuộc về một ca; tại một thời điểm chỉ có MỘT ca mở —
/// quy tắc này được DB giữ bằng unique partial index, code ở đây chỉ chặn sớm cho thông báo đẹp.
/// </summary>
public sealed class Shift : Entity
{
    public const string AlreadyOpenMessage = "Đang có ca mở. Đóng ca hiện tại trước.";
    public const string AlreadyClosedMessage = "Ca này đã đóng.";

    private Shift()
    {
    }

    public string OpenedBy { get; private set; } = "";

    public DateTimeOffset OpenedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public decimal OpeningFloat { get; private set; }

    // Đối chiếu tiền lúc đóng ca — null khi ca còn mở.
    public decimal? CountedCash { get; private set; }

    public decimal? ExpectedCash { get; private set; }

    public decimal? Variance { get; private set; }

    /// <summary>Phiên bản dòng (xmin) — hai người bấm đóng ca cùng lúc thì chỉ một người thắng.</summary>
    public uint Version { get; private set; }

    public bool IsOpen => ClosedAt is null;

    /// <param name="current">Ca đang mở (nếu có) — còn ca mở thì không mở ca mới.</param>
    public static Shift Open(Shift? current, string openedBy, decimal openingFloat, DateTimeOffset now)
    {
        if (current is { IsOpen: true })
        {
            throw new DomainException(AlreadyOpenMessage);
        }

        var shift = new Shift { OpenedBy = openedBy, OpeningFloat = openingFloat, OpenedAt = now };
        shift.Raise(new ShiftOpened(shift.Id, now));
        return shift;
    }

    /// <summary>
    /// Đóng ca và đối chiếu két: tiền phải có = quỹ đầu ca + tiền mặt đã thu; lệch = đếm được − phải có
    /// (âm là thiếu). Chuyển khoản không nằm trong két nên không vào phép tính lệch.
    /// </summary>
    public void Close(decimal countedCash, decimal cashTotal, decimal transferTotal, DateTimeOffset now)
    {
        if (!IsOpen)
        {
            throw new DomainException(AlreadyClosedMessage);
        }

        ClosedAt = now;
        CountedCash = countedCash;
        ExpectedCash = OpeningFloat + cashTotal;
        Variance = countedCash - ExpectedCash;
        Raise(new ShiftClosed(Id, now, countedCash, ExpectedCash.Value, Variance.Value, cashTotal, transferTotal));
    }
}

[Topic(Topics.ShiftOpened)]
public sealed record ShiftOpened(Guid ShiftId, DateTimeOffset OpenedAt) : IntegrationEvent
{
    public override string PartitionKey => ShiftId.ToString();
}

[Topic(Topics.ShiftClosed)]
/// <summary>Các trường tiền thêm ở lát B cho reporting (lát D); ordering chỉ đọc hai trường đầu.</summary>
public sealed record ShiftClosed(
    Guid ShiftId,
    DateTimeOffset ClosedAt,
    decimal CountedCash,
    decimal ExpectedCash,
    decimal Variance,
    decimal CashTotal,
    decimal TransferTotal) : IntegrationEvent
{
    public override string PartitionKey => ShiftId.ToString();
}
