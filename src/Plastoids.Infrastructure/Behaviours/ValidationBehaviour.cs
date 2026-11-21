using FluentValidation;
using MediatR;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Infrastructure.Behaviours;

/// <summary>Runs every FluentValidation validator for the request; failures become a Validation Result, never an exception.</summary>
public sealed class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var list = validators.ToList();
        if (list.Count == 0)
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in list)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count == 0)
        {
            return await next(cancellationToken);
        }

        var error = new ValidationError(failures.Select(f => new FieldError(f.PropertyName, f.ErrorCode, f.ErrorMessage)).ToArray());
        return ResultFactory.Failure<TResponse>(error);
    }
}

public sealed record FieldError(string Field, string Code, string Message);

/// <summary>Carries per-field problems; mapped to RFC 9457 "errors" by the HTTP layer.</summary>
public sealed record ValidationError(IReadOnlyList<FieldError> Fields)
    : Error("validation_failed", "One or more fields are invalid.", ErrorType.Validation);

internal static class ResultFactory
{
    public static TResponse Failure<TResponse>(Error error)
        where TResponse : Result
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)Result.Failure(error);
        }

        var valueType = typeof(TResponse).GetGenericArguments()[0];
        var method = typeof(Result).GetMethod(nameof(Result.Failure), 1, [typeof(Error)])!.MakeGenericMethod(valueType);
        return (TResponse)method.Invoke(null, [error])!;
    }
}
