using Microsoft.AspNetCore.Mvc;
using PaymentsFD.Contracts;

namespace PaymentsFD.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentBatchesController : ControllerBase
{
    [HttpPost]
    public ActionResult<SubmitBatchResponse> Submit([FromBody] SubmitBatchRequest request)
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

        var response = new SubmitBatchResponse
        {
            BatchId = Guid.NewGuid(),
            Status = "Accepted",
            SubmittedAtUtc = DateTimeOffset.UtcNow
        };

        return Ok(response);
    }
}
