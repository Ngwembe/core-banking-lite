using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.AspNetCore.Mvc;
//using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;

namespace core_banking_lite.Controllers
{
    [ApiController]
    [Route("bank")]
    public partial class BankAccountController : ControllerBase
    {
        private readonly IDbConnection _dbConnection;
        private readonly InfrastructureOptions _infraOptions;
        private readonly AmazonSimpleNotificationServiceClient _snsClient;

        public BankAccountController(IDbConnection dbConnection, IOptions<InfrastructureOptions> infraOptions)
        {
            _dbConnection = dbConnection;
            _infraOptions = infraOptions.Value ?? throw new ArgumentNullException(nameof(infraOptions));

            _snsClient = new AmazonSimpleNotificationServiceClient(RegionEndpoint.GetBySystemName(_infraOptions.Region));
        }

        [HttpPost("withdraw")]
        public IActionResult<string> Withdraw(
            [FromQuery] long accountId,
            [FromQuery] decimal amount)
        {
            if(amount <= 0)
            {
                return BadRequest("Withdrawal amount must be greater than zero");
            }


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

                    return Ok("Withdrawal successful");
                }
                else
                {
                    // In case the update fails for reasons other than a balance check
                    return BadRequest("Withdrawal failed");
                }
            }
            else
            {
                // Insufficient funds
                return BadRequest("Insufficient funds for withdrawal");
            }
        }
    }
}