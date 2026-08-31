namespace Payments.ServiceBus.Contracts;

public sealed record PaymentSubmittedMessage
{
    public Guid BatchId { get; init; }
    public Guid PaymentId { get; init; }
    public string BatchReference { get; init; } = string.Empty;
    public string PaymentReference { get; init; } = string.Empty;
    public string TreasuryAccountId { get; init; } = string.Empty;
    public string BeneficiaryName { get; init; } = string.Empty;
    public string BeneficiaryAccount { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string SettlementDate { get; init; } = string.Empty;
    public DateTimeOffset SubmittedAtUtc { get; init; }
}
