using System.Globalization;
using Cashier.Application;
using Fnb.Ordering.V1;
using Grpc.Core;
using Shared.Kernel;

namespace Cashier.Infrastructure;

/// <summary>Cổng gRPC sang ordering. Tiền đi dạng chuỗi invariant — protobuf không có decimal.</summary>
public sealed class OrderPaymentsClient(OrderPayments.OrderPaymentsClient grpc) : IOrderPayments
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(3);

    public async Task<MarkPaidResult> MarkPaidAsync(Guid orderId, decimal expectedTotal, Guid paymentId, CancellationToken ct)
    {
        try
        {
            var reply = await grpc.MarkPaidAsync(
                new MarkPaidRequest
                {
                    OrderId = orderId.ToString(),
                    ExpectedTotal = expectedTotal.ToString("0.00", CultureInfo.InvariantCulture),
                    PaymentId = paymentId.ToString(),
                },
                deadline: DateTime.UtcNow.Add(Deadline),
                cancellationToken: ct);
            return reply.ResultCase switch
            {
                MarkPaidReply.ResultOneofCase.Ok => new(MarkPaidStatus.Ok),
                MarkPaidReply.ResultOneofCase.TotalMismatch =>
                    new(MarkPaidStatus.TotalMismatch, decimal.Parse(reply.TotalMismatch.ActualTotal, CultureInfo.InvariantCulture)),
                MarkPaidReply.ResultOneofCase.AlreadyPaid => new(MarkPaidStatus.AlreadyPaid),
                MarkPaidReply.ResultOneofCase.OrderCancelled => new(MarkPaidStatus.OrderCancelled),
                _ => throw new InvalidOperationException($"MarkPaid trả kết quả lạ: {reply.ResultCase}"),
            };
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return new(MarkPaidStatus.NotFound);
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded or StatusCode.Aborted)
        {
            // Aborted = ordering hết lượt thử lại vì xung đột. Với thu ngân cũng như mất kết nối: rollback, bấm lại.
            throw new DependencyUnavailableException(PayOrderHandler.OrderingUnavailableMessage);
        }
    }
}
