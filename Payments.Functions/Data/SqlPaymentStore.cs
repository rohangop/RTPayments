using System.Data;
using Microsoft.Data.SqlClient;
using Payments.ServiceBus.Contracts;
using Payments.Functions.Models;

namespace Payments.Functions.Data;

public sealed class SqlPaymentStore : IPaymentStore
{
    private readonly string _connectionString;

    public SqlPaymentStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<PaymentCreateOrClaimResult> TryCreateOrClaimAsync(
        PaymentSubmittedMessage payment,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand("dbo.TryCreateOrClaimPayment", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@PaymentId", payment.PaymentId);
        command.Parameters.AddWithValue("@BatchId", payment.BatchId);
        command.Parameters.AddWithValue("@BatchReference", payment.BatchReference);
        command.Parameters.AddWithValue("@PaymentReference", payment.PaymentReference);
        command.Parameters.AddWithValue("@TreasuryAccountId", payment.TreasuryAccountId);
        command.Parameters.AddWithValue("@BeneficiaryName", payment.BeneficiaryName);
        command.Parameters.AddWithValue("@BeneficiaryAccount", payment.BeneficiaryAccount);
        command.Parameters.AddWithValue("@Currency", payment.Currency);
        command.Parameters.AddWithValue("@Amount", payment.Amount);
        command.Parameters.AddWithValue("@SettlementDate", payment.SettlementDate);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result?.ToString() switch
        {
            "Claimed" => PaymentCreateOrClaimResult.Claimed,
            "AlreadyProcessing" => PaymentCreateOrClaimResult.AlreadyProcessing,
            "AlreadySucceeded" => PaymentCreateOrClaimResult.AlreadySucceeded,
            _ => throw new InvalidOperationException("Unknown payment claim result.")
        };
    }

    public Task RecordSuccessAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteUpdateAsync(
            "dbo.RecordPaymentSuccess",
            paymentId,
            null,
            cancellationToken);
    }

    public Task RecordFailureAsync(
        Guid paymentId,
        string failureReason,
        CancellationToken cancellationToken = default)
    {
        return ExecuteUpdateAsync(
            "dbo.RecordPaymentFailure",
            paymentId,
            failureReason,
            cancellationToken);
    }

    private async Task ExecuteUpdateAsync(
        string procedureName,
        Guid paymentId,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(procedureName, connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@PaymentId", paymentId);

        if (failureReason is not null)
        {
            command.Parameters.AddWithValue("@FailureReason", failureReason);
        }

        await command.ExecuteScalarAsync(cancellationToken);
    }
}
