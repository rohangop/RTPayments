using Payments.ServiceBus.Contracts;

namespace Payments.ServiceBus;

public interface IPaymentBatchPublisher
{
    Task PublishAsync(IEnumerable<PaymentSubmittedMessage> messages, CancellationToken cancellationToken = default);
}
