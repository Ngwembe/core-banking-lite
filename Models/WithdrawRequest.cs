namespace core_banking_lite.Models
{
    public record BalanceRequest(long AccountId);
    public record WithdrawRequest(long AccountId, decimal Amount);
}
