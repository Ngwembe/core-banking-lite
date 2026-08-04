using System.Text.Json;

namespace core_banking_lite.Models.Events
{
    public class WithdrawalEvent
    {
        public string IdempotencyKey { get; }

        public decimal Amount { get; }
        public long AccountId { get; }
        public string Status { get; }

        public WithdrawalEvent(decimal amount, long accountId, string status, string idempotencyKey)
        {
            Amount = amount;
            AccountId = accountId;
            Status = status;
            IdempotencyKey = idempotencyKey;
        }

        // Convert to JSON String
        public string ToJson()
        {
            return JsonSerializer.Serialize(new
            {
                amount = Amount,
                accountId = AccountId,
                status = Status,
                idempotencyKey = IdempotencyKey
            }, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
    }
}