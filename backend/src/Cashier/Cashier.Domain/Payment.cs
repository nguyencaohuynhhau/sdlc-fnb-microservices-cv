namespace Cashier.Domain;

public enum PaymentMethod
{
    Cash,
    Transfer,
}

/// <summary>
/// Spec giữ ba trạng thái; thực tế chỉ <see cref="Completed"/> được lưu — Pending chỉ tồn tại
/// trong transaction chưa commit (xem docs/database/schema.md).
/// </summary>
public enum PaymentStatus
{
    Pending,
    Completed,
    Failed,
}

/// <summary>
/// Bút toán thu tiền một đơn. <see cref="Id"/> = <c>Idempotency-Key</c> của request, cũng là
/// <c>paymentId</c> gửi sang ordering — nhờ vậy bấm lại sau khi mất phản hồi vẫn khớp đúng lần thu cũ.
/// </summary>
public sealed class Payment
{
    private Payment()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid ShiftId { get; private set; }

    public decimal Amount { get; private set; }

    public PaymentMethod Method { get; private set; }

    public PaymentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Payment Completed(Guid id, Guid orderId, Guid shiftId, decimal amount, PaymentMethod method, DateTimeOffset now) =>
        new() { Id = id, OrderId = orderId, ShiftId = shiftId, Amount = amount, Method = method, Status = PaymentStatus.Completed, CreatedAt = now };
}
