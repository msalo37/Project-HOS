using System;

namespace HOS.Domain.Common
{
    public readonly struct Result<TValue, TError>
    {
        private readonly TValue value;
        private readonly TError error;

        private Result(bool isSuccess, TValue value, TError error)
        {
            IsSuccess = isSuccess;
            this.value = value;
            this.error = error;
        }

        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;

        public TValue Value => IsSuccess
            ? value
            : throw new InvalidOperationException("A failed result has no value.");

        public TError Error => IsFailure
            ? error
            : throw new InvalidOperationException("A successful result has no error.");

        public static Result<TValue, TError> Success(TValue value)
        {
            return new Result<TValue, TError>(true, value, default);
        }

        public static Result<TValue, TError> Failure(TError error)
        {
            return new Result<TValue, TError>(false, default, error);
        }
    }

    public static class Result
    {
        public static Result<TValue, TError> Success<TValue, TError>(TValue value)
        {
            return Result<TValue, TError>.Success(value);
        }

        public static Result<TValue, TError> Failure<TValue, TError>(TError error)
        {
            return Result<TValue, TError>.Failure(error);
        }
    }
}
