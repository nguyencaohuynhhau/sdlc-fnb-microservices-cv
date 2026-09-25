using Cashier.Domain;
using FluentAssertions;
using Shared.Kernel;

namespace Cashier.UnitTests;

public sealed class ShiftTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 18, 0, 0, TimeSpan.FromHours(7));

    [Fact]
    public void Open_NoCurrentShift_RaisesShiftOpened()
    {
        var shift = Shift.Open(null, "cashier", 500_000m, Now);

        shift.IsOpen.Should().BeTrue();
        shift.OpeningFloat.Should().Be(500_000m);
        shift.Events.Should().ContainSingle().Which.Should().BeOfType<ShiftOpened>()
            .Which.Should().Match<ShiftOpened>(e => e.ShiftId == shift.Id && e.OpenedAt == Now);
    }

    [Fact]
    public void Open_WhileAnotherShiftOpen_Throws()
    {
        var current = Shift.Open(null, "cashier", 0m, Now);

        var act = () => Shift.Open(current, "owner", 0m, Now.AddHours(1));

        act.Should().Throw<DomainException>().WithMessage(Shift.AlreadyOpenMessage);
    }

    [Fact]
    public void Open_AfterPreviousShiftClosed_Succeeds()
    {
        var previous = Shift.Open(null, "cashier", 0m, Now);
        previous.Close(0m, 0m, 0m, Now.AddHours(8));

        Shift.Open(previous, "cashier", 0m, Now.AddHours(9)).IsOpen.Should().BeTrue();
    }

    [Fact]
    public void Close_OpenShift_SetsClosedAtAndRaisesShiftClosed()
    {
        var shift = Shift.Open(null, "cashier", 0m, Now);
        shift.ClearEvents();

        shift.Close(0m, 0m, 0m, Now.AddHours(8));

        shift.ClosedAt.Should().Be(Now.AddHours(8));
        shift.Events.Should().ContainSingle().Which.Should().BeOfType<ShiftClosed>()
            .Which.ShiftId.Should().Be(shift.Id);
    }

    [Fact]
    public void Close_ComputesExpectedAndVariance()
    {
        var shift = Shift.Open(null, "cashier", 500_000m, Now);
        shift.ClearEvents();

        // Quỹ đầu 500k + thu tiền mặt 120k = két phải có 620k; đếm được 615k → thiếu 5k. Chuyển khoản không vào két.
        shift.Close(countedCash: 615_000m, cashTotal: 120_000m, transferTotal: 80_000m, Now.AddHours(8));

        shift.ExpectedCash.Should().Be(620_000m);
        shift.CountedCash.Should().Be(615_000m);
        shift.Variance.Should().Be(-5_000m);
        shift.Events.Should().ContainSingle().Which.Should().BeEquivalentTo(new ShiftClosed(
            shift.Id, Now.AddHours(8), 615_000m, 620_000m, -5_000m, 120_000m, 80_000m), o => o.Excluding(e => e.MessageId).Excluding(e => e.OccurredAt));
    }

    [Fact]
    public void Close_AlreadyClosed_Throws()
    {
        var shift = Shift.Open(null, "cashier", 0m, Now);
        shift.Close(0m, 0m, 0m, Now.AddHours(8));

        var act = () => shift.Close(0m, 0m, 0m, Now.AddHours(9));

        act.Should().Throw<DomainException>().WithMessage(Shift.AlreadyClosedMessage);
    }
}
