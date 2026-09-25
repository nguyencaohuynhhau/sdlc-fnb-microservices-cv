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
        previous.Close(Now.AddHours(8));

        Shift.Open(previous, "cashier", 0m, Now.AddHours(9)).IsOpen.Should().BeTrue();
    }

    [Fact]
    public void Close_OpenShift_SetsClosedAtAndRaisesShiftClosed()
    {
        var shift = Shift.Open(null, "cashier", 0m, Now);
        shift.ClearEvents();

        shift.Close(Now.AddHours(8));

        shift.ClosedAt.Should().Be(Now.AddHours(8));
        shift.Events.Should().ContainSingle().Which.Should().BeOfType<ShiftClosed>()
            .Which.ShiftId.Should().Be(shift.Id);
    }

    [Fact]
    public void Close_AlreadyClosed_Throws()
    {
        var shift = Shift.Open(null, "cashier", 0m, Now);
        shift.Close(Now.AddHours(8));

        var act = () => shift.Close(Now.AddHours(9));

        act.Should().Throw<DomainException>().WithMessage(Shift.AlreadyClosedMessage);
    }
}
