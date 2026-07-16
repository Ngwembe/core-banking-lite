using System.Text.Json;

namespace core_banking_lite.Models.Events
{
    public class WithdrawalEvent
    {
        public decimal Amount { get; }
        public long AccountId { get; }
        public string Status { get; }

        public WithdrawalEvent(decimal amount, long accountId, string status)
        {
            Amount = amount;
            AccountId = accountId;
            Status = status;
        }

        // Convert to JSON String
        public string ToJson()
        {
            return JsonSerializer.Serialize(new
            {
                amount = Amount,
                accountId = AccountId,
                status = Status
            });
        }
    }
}