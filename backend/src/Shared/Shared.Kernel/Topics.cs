namespace Shared.Kernel;

/// <summary>Tên topic Kafka — hợp đồng công khai giữa các dịch vụ (spec §4 "Sự kiện Kafka").</summary>
public static class Topics
{
    public const string ShiftOpened = "fnb.cashier.shift-opened.v1";
    public const string ShiftClosed = "fnb.cashier.shift-closed.v1";
    public const string OrderCancelled = "fnb.ordering.order-cancelled.v1";
}
