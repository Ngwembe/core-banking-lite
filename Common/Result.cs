using Microsoft.AspNetCore.Mvc;

namespace core_banking_lite.Common
{
    public sealed class Result<T>
    {
        public T? Value { get; }
        public string? Error { get; }
        public bool IsSuccess { get; }

        private Result(T value) { Value = value; IsSuccess = true; }
        private Result(string error) { Error = error; IsSuccess = false; }

        public static Result<T> Ok(T value) => new(value);
        public static Result<T> Fail(string error) => new(error);

        /// <summary>
        /// Railway-oriented bind — short-circuits on failure, chains on success.
        /// </summary>
        public async Task<Result<TNext>> BindAsync<TNext>(Func<T, Task<Result<TNext>>> next)
            => IsSuccess ? await next(Value!) : Result<TNext>.Fail(Error!);

        /// <summary>
        /// Terminal bind — maps a successful result to an IActionResult.
        /// </summary>
        public IActionResult Match(
            Func<T, IActionResult> onSuccess,
            Func<string, IActionResult> onFailure)
            => IsSuccess ? onSuccess(Value!) : onFailure(Error!);
    }

    /// <summary>
    /// Extension methods on Task&lt;Result&lt;T&gt;&gt; that enable fluent railway chaining
    /// directly on async-returning methods without intermediate awaits.
    /// </summary>
    public static class ResultTaskExtensions
    {
        /// <summary>
        /// Awaits the task, then binds the resolved Result into the next async step.
        /// Short-circuits on failure — next is never invoked if the result has an error.
        /// This is what makes the chain in the controller compile:
        ///   ValidateInput(...)          → Task&lt;Result&lt;T&gt;&gt;
        ///     .BindAsync(...)           → Task&lt;Result&lt;TNext&gt;&gt;
        /// </summary>
        public static async Task<Result<TNext>> BindAsync<T, TNext>(
            this Task<Result<T>> resultTask,
            Func<T, Task<Result<TNext>>> next)
        {
            Result<T> result = await resultTask;
            return result.IsSuccess
                ? await next(result.Value!)
                : Result<TNext>.Fail(result.Error!);
        }

        /// <summary>
        /// Awaits the task, then executes Match on the resolved Result.
        /// Allows the entire railway expression to terminate in a single fluent call.
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

    /// <summary>Represents a void result in the railway chain.</summary>
    public sealed record Unit
    {
        public static readonly Unit Value = new();
    }
}
