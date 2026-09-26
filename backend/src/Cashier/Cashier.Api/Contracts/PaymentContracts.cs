using System.ComponentModel.DataAnnotations;
using Cashier.Domain;

namespace Cashier.Api.Contracts;

public sealed record CreatePaymentRequest(
    [Required] Guid OrderId,
    [Required, Range(0.01, 1_000_000_000)] decimal ExpectedTotal,
    [Required] PaymentMethod Method);
