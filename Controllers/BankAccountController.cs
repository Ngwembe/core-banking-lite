using core_banking_lite.Common;
using core_banking_lite.Entities;
using core_banking_lite.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace core_banking_lite.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public partial class BankAccountController : ControllerBase
    {
        private readonly ILogger<BankAccountController> _logger;
        private readonly IBankAccountRepository _repository;

        public BankAccountController(ILogger<BankAccountController> logger, IBankAccountRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        [HttpGet("getbalance")]
        public async Task<IActionResult> GetBalance([FromQuery] long accountId, CancellationToken cancellationToken)
        {
            if (accountId <= 0)
                return BadRequest(new { error = "Account ID must be positive." });
            decimal? balance = await _repository.GetBalanceAsync(accountId, cancellationToken);
            if (balance is null)
                return NotFound(new { error = $"Account with ID {accountId} not found or inactive." });
            return Ok(new { accountId, balance });
        }

        [HttpPost("withdraw")]
        public async Task<IActionResult> Withdraw(
        [FromQuery] long accountId,
        [FromQuery] decimal amount,
        CancellationToken cancellationToken)
        {
            return await ValidateInput(accountId, amount)
                .BindAsync(input => ProcessWithdrawalAsync(input.AccountId, input.Amount, cancellationToken))
                .MatchAsync(
                    onSuccess: record =>
                    {
                        _logger.LogInformation(
                            "Withdrawal of {Amount} completed for account {AccountId}.", record.Amount, record.AccountId);
                        return Ok(new { message = "Withdrawal successful.", record.AccountId, record.Amount });
                    },
                    onFailure: error =>
                    {
                        _logger.LogWarning("Withdrawal rejected: {Error}", error);
                        return error.Contains("not found") || error.Contains("funds")
                            ? UnprocessableEntity(new { error })
                            : BadRequest(new { error });
                    });
        }

        private static Task<Result<(long AccountId, decimal Amount)>> ValidateInput(long accountId, decimal amount)
        {
            if (accountId <= 0)
                return Task.FromResult(Result<(long, decimal)>.Fail("Account ID must be positive."));

            if (amount <= 0)
                return Task.FromResult(Result<(long, decimal)>.Fail("Withdrawal amount must be greater than zero."));

            return Task.FromResult(Result<(long, decimal)>.Ok((accountId, amount)));
        }

        private Task<Result<WithdrawalRecord>> ProcessWithdrawalAsync(long accountId, decimal amount, CancellationToken ct)
            => _repository.DeductBalanceAndEnqueueEventAsync(accountId, amount, ct);
    }
}