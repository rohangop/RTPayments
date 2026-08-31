namespace Payments.Functions.Models;

public enum PaymentCreateOrClaimResult
{
    Claimed,
    AlreadyProcessing,
    AlreadySucceeded
}
