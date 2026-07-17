namespace core_banking_lite.Entities
{
    public sealed record WithdrawalRecord(long AccountId, decimal Amount, DateTime ProcessedAt);
}
