using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Payments.ServiceBus.Contracts;

namespace Payments.ServiceBus;

public sealed class ServiceBusPaymentBatchPublisher : IPaymentBatchPublisher
{
    private readonly ServiceBusClient _client;
    private readonly string _queueName;

    public ServiceBusPaymentBatchPublisher(ServiceBusClient client, string queueName)
    {
        _client = client;
        _queueName = queueName;
    }

    public async Task PublishAsync(IEnumerable<PaymentSubmittedMessage> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        // We batch messages together to minimize network hops while still sending one payment per unit of work.
        var sender = _client.CreateSender(_queueName);
        var messageList = messages.ToList();

        if (messageList.Count == 0)
        {
            return;
        }

        ServiceBusMessageBatch? batch = null;

        foreach (var message in messageList)
        {
            var payload = JsonSerializer.Serialize(message);
            var serviceBusMessage = new ServiceBusMessage(payload);

            if (batch is null)
            {
                batch = await sender.CreateMessageBatchAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!batch.TryAddMessage(serviceBusMessage))
            {
                // The current Service Bus batch is full, so we flush it before adding the next message.
                await sender.SendMessagesAsync(batch, cancellationToken).ConfigureAwait(false);
                batch = await sender.CreateMessageBatchAsync(cancellationToken).ConfigureAwait(false);

                if (!batch.TryAddMessage(serviceBusMessage))
                {
                    throw new InvalidOperationException("Payment message exceeds the maximum size allowed for a Service Bus batch.");
                }
            }
        }

        if (batch is not null && batch.Count > 0)
        {
            await sender.SendMessagesAsync(batch, cancellationToken).ConfigureAwait(false);
        }
    }
}
