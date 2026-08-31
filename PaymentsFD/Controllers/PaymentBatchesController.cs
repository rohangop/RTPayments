using Microsoft.AspNetCore.Mvc;
using Payments.ServiceBus;
using Payments.ServiceBus.Contracts;
using PaymentsFD.Contracts;

namespace PaymentsFD.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentBatchesController : ControllerBase
{
    private const int MaximumPaymentsPerBatch = 100;
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
        if (request.BatchId == Guid.Empty)
        {
            return BadRequest(new { error = "BatchId is required." });
        }

        if (string.IsNullOrWhiteSpace(request.TreasuryAccountId))
        {
            return BadRequest(new { error = "TreasuryAccountId is required." });
        }

        if (request.Payments.Count == 0)
        {
            return BadRequest(new { error = "At least one payment is required." });
        }

        if (request.Payments.Count > MaximumPaymentsPerBatch)
        {
            return BadRequest(new
            {
                error = $"A batch cannot contain more than {MaximumPaymentsPerBatch} payments."
            });
        }

        if (request.Payments.Any(payment => payment.PaymentId == Guid.Empty))
        {
            return BadRequest(new { error = "Every payment must include a PaymentId." });
        }

        if (request.Payments.Select(payment => payment.PaymentId).Distinct().Count() != request.Payments.Count)
        {
            return BadRequest(new { error = "PaymentId values must be unique within a batch." });
        }

        var batchId = request.BatchId;
        var submittedAtUtc = DateTimeOffset.UtcNow;
        var messages = request.Payments.Select(payment => new PaymentSubmittedMessage
        {
            BatchId = batchId,
            PaymentId = payment.PaymentId,
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
