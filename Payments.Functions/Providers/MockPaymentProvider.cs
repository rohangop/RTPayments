using Payments.ServiceBus.Contracts;

namespace Payments.Functions.Providers;

public sealed class MockPaymentProvider : IPaymentProvider
{
    public Task<PaymentProviderResult> SubmitAsync(
        PaymentSubmittedMessage payment,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PaymentProviderResult(Succeeded: true));
    }
}
