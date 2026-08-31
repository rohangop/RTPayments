using Payments.ServiceBus.Contracts;
using Payments.Functions.Models;

namespace Payments.Functions.Data;

public interface IPaymentStore
{
    Task<PaymentCreateOrClaimResult> TryCreateOrClaimAsync(
        PaymentSubmittedMessage payment,
        CancellationToken cancellationToken = default);

    Task RecordSuccessAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default);

    Task RecordFailureAsync(
        Guid paymentId,
        string failureReason,
        CancellationToken cancellationToken = default);
}
