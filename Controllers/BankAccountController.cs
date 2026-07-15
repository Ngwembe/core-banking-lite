using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace core_banking_lite.Controllers
{
    [ApiController]
    [Route("bank")]
    public partial class BankAccountController : ControllerBase
    {
        private readonly IDbConnection _dbConnection;
        private readonly AmazonSimpleNotificationServiceClient _snsClient;

        public BankAccountController(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;

            _snsClient = new AmazonSimpleNotificationServiceClient(
                RegionEndpoint.YOUR_REGION // Specify your AWS region
            );
        }

        [HttpPost("withdraw")]
        public string Withdraw(
            [FromQuery] long accountId,
            [FromQuery] decimal amount)
        {
            // Check current balance
            string sql = "SELECT balance FROM accounts WHERE id = @accountId";

            using var command = _dbConnection.CreateCommand();
            command.CommandText = sql;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@accountId";
            parameter.Value = accountId;
            command.Parameters.Add(parameter);

            if (_dbConnection.State != ConnectionState.Open)
                _dbConnection.Open();

            object? result = command.ExecuteScalar();

            decimal? currentBalance = result == null || result == DBNull.Value
                ? null
                : Convert.ToDecimal(result);

            if (currentBalance != null && currentBalance >= amount)
            {
                // Update balance
                sql = "UPDATE accounts SET balance = balance - @amount WHERE id = @accountId";

                using var updateCommand = _dbConnection.CreateCommand();
                updateCommand.CommandText = sql;

                var amountParameter = updateCommand.CreateParameter();
                amountParameter.ParameterName = "@amount";
                amountParameter.Value = amount;
                updateCommand.Parameters.Add(amountParameter);

                var accountParameter = updateCommand.CreateParameter();
                accountParameter.ParameterName = "@accountId";
                accountParameter.Value = accountId;
                updateCommand.Parameters.Add(accountParameter);

                int rowsAffected = updateCommand.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    return "Withdrawal successful";
                }
                else
                {
                    // In case the update fails for reasons other than a balance check
                    return "Withdrawal failed";
                }
            }
            else
            {
                // Insufficient funds
                return "Insufficient funds for withdrawal";
            }

            // After a successful withdrawal, publish a withdrawal event to SNS
            WithdrawalEvent @event = new WithdrawalEvent(amount, accountId, "SUCCESSFUL");

            string eventJson = @event.ToJson();

            string snsTopicArn =
                "arn:aws:sns:YOUR_REGION:YOUR_ACCOUNT_ID:YOUR_TOPIC_NAME";

            PublishRequest publishRequest = new PublishRequest
            {
                Message = eventJson,
                TopicArn = snsTopicArn
            };

            PublishResponse publishResponse = _snsClient.PublishAsync(publishRequest).Result;

            return "Withdrawal successful";
        }
    }
}