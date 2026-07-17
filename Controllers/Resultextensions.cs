using core_banking_lite.Common;
using Microsoft.AspNetCore.Mvc;

namespace core_banking_lite.Controllers
{
    public static class ResultExtensions
    {
        /// <summary>
        /// Awaits the task, then maps the resolved Result to an IActionResult.
        /// This is the terminal step of the railway chain in controllers.
        /// </summary>
        public static async Task<IActionResult> MatchAsync<T>(
            this Task<Result<T>> resultTask,
            Func<T, IActionResult> onSuccess,
            Func<string, IActionResult> onFailure)
        {
            Result<T> result = await resultTask;
            return result.Match(onSuccess, onFailure);
        }
    }
}
