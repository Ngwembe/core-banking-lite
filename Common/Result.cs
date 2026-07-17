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
        /// Maps the result to any output type — no framework dependency.
        /// </summary>
        public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<string, TOut> onFailure)
            => IsSuccess ? onSuccess(Value!) : onFailure(Error!);
    }

    /// <summary>
    /// Framework-agnostic async chaining extensions on Task&lt;Result&lt;T&gt;&gt;.
    /// HTTP-specific overloads live in Controllers\ResultExtensions.cs.
    /// </summary>
    public static class ResultTaskExtensions
    {
        /// <summary>
        /// Awaits the task, then binds the resolved Result into the next async step.
        /// Short-circuits on failure — next is never invoked if the result has an error.
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
        /// Awaits the task, then maps the resolved Result to any output type.
        /// </summary>
        public static async Task<TOut> MatchAsync<T, TOut>(
            this Task<Result<T>> resultTask,
            Func<T, TOut> onSuccess,
            Func<string, TOut> onFailure)
        {
            Result<T> result = await resultTask;
            return result.Match(onSuccess, onFailure);
        }
    }

    /// <summary>Represents a void success value in the railway chain.</summary>
    public sealed record Unit
    {
        public static readonly Unit Value = new();
    }
}
