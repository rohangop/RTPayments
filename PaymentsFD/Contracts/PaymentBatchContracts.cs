namespace PaymentsFD.Contracts;

public sealed record SubmitBatchRequest
{
    public string BatchReference { get; init; } = string.Empty;
    public string TreasuryAccountId { get; init; } = string.Empty;
    public string SettlementDate { get; init; } = string.Empty;
    public List<PaymentInstruction> Payments { get; init; } = new();
}

public sealed record PaymentInstruction
{
    public string PaymentReference { get; init; } = string.Empty;
    public string BeneficiaryName { get; init; } = string.Empty;
    public string BeneficiaryAccount { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? Description { get; init; }
}

public sealed record SubmitBatchResponse
{
    public Guid BatchId { get; init; }
    public string Status { get; init; } = "Accepted";
    public DateTimeOffset SubmittedAtUtc { get; init; }
}
