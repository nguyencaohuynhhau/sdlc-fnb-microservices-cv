using FluentAssertions;
using Ordering.Domain;
using Shared.Kernel;

namespace Ordering.UnitTests;

public sealed class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 9, 0, 0, TimeSpan.Zero);
    private static readonly MenuItem Latte = new("Latte", 45_000m);
    private static readonly MenuItem Croissant = new("Croissant", 30_000m);

    private static Order NewOrder() => Order.Create(Guid.NewGuid(), [(Latte, 2), (Croissant, 1)], Now);

    [Fact]
    public void Create_SnapshotsNameAndPrice_AndTotalsLines()
    {
        var order = NewOrder();

        order.Total.Should().Be(120_000m);
        order.Items.Select(i => (i.Name, i.UnitPrice, i.Qty, i.Line)).Should().Equal(("Latte", 45_000m, 2, 1), ("Croissant", 30_000m, 1, 2));
        order.Items.Should().OnlyContain(i => i.Status == OrderItemStatus.Pending);
    }

    [Fact]
    public void Create_WithSoldOutItem_ThrowsWithItemName()
    {
        var soldOut = new MenuItem("Bạc xỉu", 35_000m);
        soldOut.SetAvailable(false);

        var act = () => Order.Create(Guid.NewGuid(), [(Latte, 1), (soldOut, 1)], Now);

        act.Should().Throw<DomainException>().WithMessage("Món Bạc xỉu vừa hết hàng, vui lòng bỏ khỏi đơn.");
    }

    [Fact]
    public void CancelItem_Pending_ExcludedFromTotal()
    {
        var order = NewOrder();

        order.CancelItem(order.Items[0].Id, Now.AddMinutes(1));

        order.Items[0].Status.Should().Be(OrderItemStatus.Cancelled);
        order.Total.Should().Be(30_000m);
        order.UpdatedAt.Should().Be(Now.AddMinutes(1));
    }

    [Theory]
    [InlineData(OrderItemStatus.Preparing)]
    [InlineData(OrderItemStatus.Done)]
    public void CancelItem_KitchenAlreadyStarted_Throws(OrderItemStatus reached)
    {
        var order = NewOrder();
        var item = order.Items[0];
        order.SetItemStatus(item.Id, OrderItemStatus.Preparing, Now);
        if (reached == OrderItemStatus.Done)
        {
            order.SetItemStatus(item.Id, OrderItemStatus.Done, Now);
        }

        var act = () => order.CancelItem(item.Id, Now);

        act.Should().Throw<DomainException>().WithMessage(Order.ItemAlreadyCookedMessage);
        order.Total.Should().Be(120_000m);
    }

    [Fact]
    public void SetItemStatus_InOrder_Succeeds()
    {
        var order = NewOrder();
        var id = order.Items[0].Id;

        order.SetItemStatus(id, OrderItemStatus.Preparing, Now);
        order.SetItemStatus(id, OrderItemStatus.Done, Now);

        order.Items[0].Status.Should().Be(OrderItemStatus.Done);
    }

    [Theory]
    [InlineData(OrderItemStatus.Pending, OrderItemStatus.Done)] // nhảy cóc
    [InlineData(OrderItemStatus.Preparing, OrderItemStatus.Pending)] // lùi
    [InlineData(OrderItemStatus.Pending, OrderItemStatus.Cancelled)] // huỷ không đi đường này
    [InlineData(OrderItemStatus.Pending, OrderItemStatus.Pending)]
    public void SetItemStatus_OutOfSequence_Throws(OrderItemStatus from, OrderItemStatus to)
    {
        var order = NewOrder();
        var id = order.Items[0].Id;
        if (from == OrderItemStatus.Preparing)
        {
            order.SetItemStatus(id, OrderItemStatus.Preparing, Now);
        }

        var act = () => order.SetItemStatus(id, to, Now);

        act.Should().Throw<DomainException>().WithMessage(Order.StatusSequenceMessage);
    }

    [Fact]
    public void Cancel_Open_RaisesOrderCancelled_ThenSecondCancelThrows()
    {
        var order = NewOrder();

        order.Cancel(Now);

        order.Status.Should().Be(OrderStatus.Cancelled);
        order.Events.Should().ContainSingle().Which.Should().BeOfType<OrderCancelled>().Which.OrderId.Should().Be(order.Id);
        var again = () => order.Cancel(Now);
        again.Should().Throw<DomainException>().WithMessage(Order.OnlyOpenCanCancelMessage);
    }

    [Fact]
    public void CancelledOrder_RejectsAddAndKitchenChanges()
    {
        var order = NewOrder();
        order.Cancel(Now);

        var add = () => order.AddItem(Latte, 1, Now);
        var cook = () => order.SetItemStatus(order.Items[0].Id, OrderItemStatus.Preparing, Now);

        add.Should().Throw<DomainException>().WithMessage(Order.ClosedForAddMessage);
        cook.Should().Throw<DomainException>().WithMessage(Order.ClosedMessage);
    }
}
