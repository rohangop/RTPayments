using Microsoft.AspNetCore.Mvc;
using Payments.ServiceBus;
using Payments.ServiceBus.Contracts;
using PaymentsFD.Contracts;

namespace PaymentsFD.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentBatchesController : ControllerBase
{
    private readonly IPaymentBatchPublisher _publisher;

    public PaymentBatchesController(IPaymentBatchPublisher publisher)
    {
        _publisher = publisher;
    }

    [HttpPost]
    public async Task<ActionResult<SubmitBatchResponse>> Submit(
        [FromBody] SubmitBatchRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BatchReference))
        {
            return BadRequest(new { error = "BatchReference is required." });
        }

        if (string.IsNullOrWhiteSpace(request.TreasuryAccountId))
        {
            return BadRequest(new { error = "TreasuryAccountId is required." });
        }

        if (request.Payments.Count == 0)
        {
            return BadRequest(new { error = "At least one payment is required." });
        }

        var batchId = Guid.NewGuid();
        var submittedAtUtc = DateTimeOffset.UtcNow;
        var messages = request.Payments.Select(payment => new PaymentSubmittedMessage
        {
            BatchId = batchId,
            PaymentId = Guid.NewGuid(),
            BatchReference = request.BatchReference,
            PaymentReference = payment.PaymentReference,
            TreasuryAccountId = request.TreasuryAccountId,
            BeneficiaryName = payment.BeneficiaryName,
            BeneficiaryAccount = payment.BeneficiaryAccount,
            Currency = payment.Currency,
            Amount = payment.Amount,
            SettlementDate = request.SettlementDate,
            SubmittedAtUtc = submittedAtUtc
        });

        await _publisher.PublishAsync(messages, cancellationToken);

        var response = new SubmitBatchResponse
        {
            BatchId = batchId,
            Status = "Accepted",
            SubmittedAtUtc = submittedAtUtc
        };

        return Accepted(response);
    }
}
