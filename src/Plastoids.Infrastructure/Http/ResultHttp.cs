using Microsoft.AspNetCore.Http;
using Plastoids.Infrastructure.Behaviours;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Infrastructure.Http;

/// <summary>Maps <see cref="Result"/> to HTTP: success → 200/201/204, failure → RFC 9457 problem details with a stable "code".</summary>
public static class ResultHttp
{
    public static IResult ToHttpResult(this Result result, int successStatus = StatusCodes.Status204NoContent)
        => result.IsSuccess ? Results.StatusCode(successStatus) : Problem(result.Error);

    public static IResult ToHttpResult<T>(this Result<T> result, int successStatus = StatusCodes.Status200OK)
        => result.IsSuccess ? Results.Json(result.Value, statusCode: successStatus) : Problem(result.Error);

    public static IResult ToCreatedResult<T>(this Result<T> result, Func<T, string> location)
        => result.IsSuccess ? Results.Created(location(result.Value), result.Value) : Problem(result.Error);

    public static IResult Problem(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status422UnprocessableEntity,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError,
        };

        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };
        if (error is ValidationError v)
        {
            extensions["errors"] = v.Fields.GroupBy(f => f.Field).ToDictionary(g => g.Key, g => g.Select(f => new { f.Code, f.Message }).ToArray());
        }

        return Results.Problem(
            title: error.Code,
            detail: error.Message,
            statusCode: status,
            type: $"https://docs.plastoids.ng/errors/{error.Code}",
            extensions: extensions);
    }
}
