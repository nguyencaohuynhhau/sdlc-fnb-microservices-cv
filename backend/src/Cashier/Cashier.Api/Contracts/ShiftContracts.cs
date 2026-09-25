using System.ComponentModel.DataAnnotations;
using Cashier.Application;
using Cashier.Domain;

namespace Cashier.Api.Contracts;

public sealed record OpenShiftRequest([Range(typeof(decimal), "0", "1000000000")] decimal OpeningFloat);

/// <summary>Nullable + Required: thiếu <c>countedCash</c> là 400, không lặng lẽ thành 0 rồi báo lệch két.</summary>
public sealed record CloseShiftRequest([Required, Range(typeof(decimal), "0", "1000000000")] decimal? CountedCash);

/// <summary>Kết quả đóng ca: đối chiếu két + doanh thu (tiêu chí #8).</summary>
public sealed record ShiftSummaryResponse(
    Guid Id,
    string OpenedBy,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    decimal OpeningFloat,
    decimal? CountedCash,
    decimal? ExpectedCash,
    decimal? Variance,
    int OrderCount,
    decimal Revenue,
    decimal CashTotal,
    decimal TransferTotal)
{
    public static ShiftSummaryResponse From(ShiftSummary x) => new(
        x.Shift.Id,
        x.Shift.OpenedBy,
        x.Shift.OpenedAt,
        x.Shift.ClosedAt,
        x.Shift.OpeningFloat,
        x.Shift.CountedCash,
        x.Shift.ExpectedCash,
        x.Shift.Variance,
        x.Totals.OrderCount,
        x.Totals.CashTotal + x.Totals.TransferTotal,
        x.Totals.CashTotal,
        x.Totals.TransferTotal);
}

public sealed record ShiftResponse(
    Guid Id,
    string OpenedBy,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    decimal OpeningFloat,
    decimal? CountedCash,
    decimal? ExpectedCash,
    decimal? Variance)
{
    public static ShiftResponse From(Shift s) =>
        new(s.Id, s.OpenedBy, s.OpenedAt, s.ClosedAt, s.OpeningFloat, s.CountedCash, s.ExpectedCash, s.Variance);
}
