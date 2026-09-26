using System.Globalization;
using Fnb.Ordering.V1;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Ordering.Application;
using Ordering.Domain;
using Shared.Kernel;

namespace Ordering.Api.Grpc;

/// <summary>
/// gRPC nội bộ cho cashier. Cùng JWT với HTTP (cashier chuyển token của thu ngân sang), nên
/// <c>FallbackPolicy</c> và vai trò vẫn áp dụng như mọi endpoint khác.
/// </summary>
[Authorize(Roles = "Cashier,Owner")]
public sealed class OrderPaymentsService(OrderService service) : OrderPayments.OrderPaymentsBase
{
    public override async Task<MarkPaidReply> MarkPaid(MarkPaidRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrderId, out var orderId)
            || !Guid.TryParse(request.PaymentId, out var paymentId)
            || !decimal.TryParse(request.ExpectedTotal, NumberStyles.Number, CultureInfo.InvariantCulture, out var expectedTotal))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "order_id, payment_id phải là uuid; expected_total là số thập phân."));
        }

        try
        {
            var (outcome, actual) = await service.MarkPaidAsync(orderId, expectedTotal, paymentId, context.CancellationToken);
            return outcome switch
            {
                MarkPaidOutcome.Ok => new MarkPaidReply { Ok = new Ok() },
                MarkPaidOutcome.TotalMismatch => new MarkPaidReply
                {
                    TotalMismatch = new TotalMismatch { ActualTotal = actual.ToString("0.00", CultureInfo.InvariantCulture) },
                },
                MarkPaidOutcome.AlreadyPaid => new MarkPaidReply { AlreadyPaid = new AlreadyPaid() },
                _ => new MarkPaidReply { OrderCancelled = new Fnb.Ordering.V1.OrderCancelled() },
            };
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (DomainException ex)
        {
            // Hết lượt thử lại vì xung đột xmin — cashier rollback, thu ngân bấm lại.
            throw new RpcException(new Status(StatusCode.Aborted, ex.Message));
        }
    }
}
