using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Payments.ServiceBus.Contracts;

namespace Payments.ServiceBus;

public sealed class ServiceBusPaymentBatchPublisher : IPaymentBatchPublisher
{
    private const int MaximumPaymentsPerBatch = 100;
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

        var sender = _client.CreateSender(_queueName);
        var messageList = messages.ToList();

        if (messageList.Count == 0)
        {
            return;
        }

        if (messageList.Count > MaximumPaymentsPerBatch)
        {
            throw new InvalidOperationException(
                $"A Service Bus batch cannot contain more than {MaximumPaymentsPerBatch} payments.");
        }

        using var batch =
            await sender.CreateMessageBatchAsync(cancellationToken).ConfigureAwait(false);

        foreach (var message in messageList)
        {
            var payload = JsonSerializer.Serialize(message);
            var serviceBusMessage = new ServiceBusMessage(payload)
            {
                MessageId = message.PaymentId.ToString(),
                CorrelationId = message.BatchId.ToString(),
                Subject = "PaymentSubmitted"
            };

            if (!batch.TryAddMessage(serviceBusMessage))
            {
                throw new InvalidOperationException(
                    "The payment request exceeds the Service Bus batch size limit.");
            }
        }

        await sender.SendMessagesAsync(batch, cancellationToken).ConfigureAwait(false);
    }
}
