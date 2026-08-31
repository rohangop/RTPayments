using Payments.ServiceBus.Contracts;

namespace Payments.Functions.Providers;

public interface IPaymentProvider
{
    Task<PaymentProviderResult> SubmitAsync(
        PaymentSubmittedMessage payment,
        CancellationToken cancellationToken = default);
}

public sealed record PaymentProviderResult(
    bool Succeeded,
    string? FailureReason = null);
