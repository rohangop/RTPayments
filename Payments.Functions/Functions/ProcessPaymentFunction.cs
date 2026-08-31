using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Payments.Functions.Functions;

public sealed class ProcessPaymentFunction
{
    private readonly ILogger<ProcessPaymentFunction> _logger;

    public ProcessPaymentFunction(ILogger<ProcessPaymentFunction> logger)
    {
        _logger = logger;
    }

    [Function(nameof(ProcessPaymentFunction))]
    public Task Run(
        [ServiceBusTrigger("payments", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message)
    {
        _logger.LogInformation(
            "Received payment message {MessageId} with delivery count {DeliveryCount}.",
            message.MessageId,
            message.DeliveryCount);

        return Task.CompletedTask;
    }
}
