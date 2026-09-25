using System.ComponentModel.DataAnnotations;
using Cashier.Domain;

namespace Cashier.Api.Contracts;

public sealed record OpenShiftRequest([Range(typeof(decimal), "0", "1000000000")] decimal OpeningFloat);

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
