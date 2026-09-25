using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cashier.Domain;
using Shared.Kernel;

namespace Cashier.Application;

public sealed record PayOrder(Guid OrderId, decimal ExpectedTotal, PaymentMethod Method);

/// <summary>Hình dạng phản hồi 200 của <c>POST /api/payments</c> (docs/api/endpoints.md).</summary>
public sealed record PaymentReceipt(Guid PaymentId, Guid OrderId, Guid ShiftId, decimal Amount, string Method, DateTimeOffset PaidAt);

/// <summary>
/// Thu tiền một đơn trong MỘT transaction: giữ key → khoá ca → gRPC MarkPaid → ghi bút toán + phản hồi → commit.
/// Lỗi ở bất kỳ bước nào rollback cả key, nên thu ngân bấm lại cùng key là chạy lại từ đầu; chỉ lần
/// thành công được lưu để trả lại nguyên văn (tiêu chí #5).
/// </summary>
public sealed class PayOrderHandler(IPaymentLedger ledger, IOrderPayments orders, TimeProvider clock)
{
    public const string Endpoint = "POST /api/payments";
    public const string NoOpenShiftMessage = "Chưa mở ca làm việc.";
    public const string KeyMismatchMessage = "Yêu cầu không khớp với lần gửi trước.";
    public const string AlreadyPaidMessage = "Đơn này đã được thanh toán.";
    public const string CancelledMessage = "Đơn đã bị huỷ, không thu tiền được.";
    public const string OrderingUnavailableMessage = "Không kết nối được dịch vụ đơn hàng. Thử lại sau giây lát.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <returns>Body JSON của phản hồi 200 — lần đầu hay gửi lại đều là cùng chuỗi này.</returns>
    public async Task<string> HandleAsync(Guid key, PayOrder cmd, CancellationToken ct)
    {
        var hash = RequestHash(cmd);
        var now = clock.GetUtcNow();
        await using var tx = await ledger.BeginAsync(ct);

        if (await ledger.ClaimKeyAsync(key, Endpoint, hash, now, ct) is { } previous)
        {
            return previous.RequestHash == hash ? previous.Body : throw new UnprocessableRequestException(KeyMismatchMessage);
        }

        var shiftId = await ledger.LockOpenShiftAsync(ct) ?? throw new DomainException(NoOpenShiftMessage);

        // paymentId = key: mất phản hồi rồi bấm lại, ordering nhận ra đúng lần thu cũ và trả Ok.
        var result = await orders.MarkPaidAsync(cmd.OrderId, cmd.ExpectedTotal, key, ct);
        switch (result.Status)
        {
            case MarkPaidStatus.TotalMismatch:
                throw new DomainException($"Đơn vừa thay đổi, tổng tiền hiện tại là {result.ActualTotal?.ToString("N0", Vi)}đ. Kiểm tra lại rồi thu tiền.");
            case MarkPaidStatus.AlreadyPaid:
                throw new DomainException(AlreadyPaidMessage);
            case MarkPaidStatus.OrderCancelled:
                throw new DomainException(CancelledMessage);
            case MarkPaidStatus.NotFound:
                throw new NotFoundException("Không tìm thấy đơn.");
        }

        var payment = Payment.Completed(key, cmd.OrderId, shiftId, cmd.ExpectedTotal, cmd.Method, now);
        var body = JsonSerializer.Serialize(
            new PaymentReceipt(payment.Id, payment.OrderId, payment.ShiftId, payment.Amount, payment.Method.ToString(), payment.CreatedAt), Json);
        await ledger.RecordAsync(payment, key, Endpoint, 200, body, ct);
        await tx.CommitAsync(ct);
        return body;
    }

    /// <summary>
    /// Băm body đã chuẩn hoá. Tiền ghi cố định 2 chữ số lẻ: <c>45000</c> và <c>45000.00</c> là
    /// cùng một yêu cầu, không được coi là "body khác" (422 oan).
    /// </summary>
    public static string RequestHash(PayOrder cmd) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{cmd.OrderId}|{cmd.ExpectedTotal.ToString("0.00", CultureInfo.InvariantCulture)}|{cmd.Method}")));
}
