using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Payments.Functions.Data;
using Payments.Functions.Models;
using Payments.Functions.Providers;
using Payments.ServiceBus.Contracts;

namespace Payments.Functions.Functions;

public sealed class ProcessPaymentFunction
{
    private readonly ILogger<ProcessPaymentFunction> _logger;
    private readonly IPaymentStore _paymentStore;
    private readonly IPaymentProvider _paymentProvider;

    public ProcessPaymentFunction(
        ILogger<ProcessPaymentFunction> logger,
        IPaymentStore paymentStore,
        IPaymentProvider paymentProvider)
    {
        _logger = logger;
        _paymentStore = paymentStore;
        _paymentProvider = paymentProvider;
    }

    [Function(nameof(ProcessPaymentFunction))]
    public async Task Run(
        [ServiceBusTrigger("payments", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received payment message {MessageId} with delivery count {DeliveryCount}.",
            message.MessageId,
            message.DeliveryCount);

        var payment = message.Body.ToObjectFromJson<PaymentSubmittedMessage>()
            ?? throw new InvalidOperationException("Payment message was empty.");

        var claimResult = await _paymentStore.TryCreateOrClaimAsync(
            payment,
            cancellationToken);

        if (claimResult == PaymentCreateOrClaimResult.AlreadySucceeded)
        {
            _logger.LogInformation(
                "Payment {PaymentId} was already completed.",
                payment.PaymentId);
            return;
        }

        if (claimResult == PaymentCreateOrClaimResult.AlreadyProcessing)
        {
            throw new InvalidOperationException(
                $"Payment {payment.PaymentId} is already processing.");
        }

        var providerResult = await _paymentProvider.SubmitAsync(
            payment,
            cancellationToken);

        if (providerResult.Succeeded)
        {
            await _paymentStore.RecordSuccessAsync(
                payment.PaymentId,
                cancellationToken);
            return;
        }

        await _paymentStore.RecordFailureAsync(
            payment.PaymentId,
            providerResult.FailureReason ?? "Payment provider rejected the payment.",
            cancellationToken);
    }
}
