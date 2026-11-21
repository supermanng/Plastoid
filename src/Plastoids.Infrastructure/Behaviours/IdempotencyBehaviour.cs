using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Infrastructure.Persistence.Shared;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Infrastructure.Behaviours;

/// <summary>
/// For <see cref="IIdempotentCommand"/>: same key + same request hash → replay stored result;
/// same key + different hash → 422 conflict. The key row is written in the command's transaction.
/// Runs INSIDE the UnitOfWorkBehaviour (registered after it).
/// </summary>
public sealed class IdempotencyBehaviour<TRequest, TResponse>(
    SharedDbContext shared,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    IClock clock)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IIdempotentCommand
    where TResponse : Result
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return ResultFactory.Failure<TResponse>(Error.Validation("idempotency_key_required", "Idempotency-Key header is required for this operation."));
        }

        var actorId = user.UserId ?? Guid.Empty;
        var hash = Hash(request);
        await unitOfWork.EnlistAsync(shared, cancellationToken);

        var existing = await shared.IdempotencyKeys
            .SingleOrDefaultAsync(k => k.Key == request.IdempotencyKey && k.ActorId == actorId, cancellationToken);

        if (existing is not null)
        {
            if (existing.RequestHash != hash)
            {
                return ResultFactory.Failure<TResponse>(Error.Conflict("idempotency_key_reused", "Idempotency-Key was already used with a different request."));
            }

            return Replay(existing);
        }

        var response = await next(cancellationToken);
        if (response.IsSuccess)
        {
            shared.IdempotencyKeys.Add(new IdempotencyKey
            {
                Key = request.IdempotencyKey,
                ActorId = actorId,
                RequestType = typeof(TRequest).FullName ?? typeof(TRequest).Name,
                RequestHash = hash,
                StatusCode = 200,
                ResponseBody = SerialiseValue(response),
                CreatedAt = clock.UtcNow,
                ExpiresAt = clock.UtcNow.Add(Ttl),
            });
        }

        return response;
    }

    private static string Hash(TRequest request)
    {
        var json = JsonSerializer.Serialize(request, request.GetType());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    private static string? SerialiseValue(TResponse response)
    {
        var valueProperty = typeof(TResponse).GetProperty("Value");
        return valueProperty is null ? null : JsonSerializer.Serialize(valueProperty.GetValue(response), valueProperty.PropertyType);
    }

    private static TResponse Replay(IdempotencyKey stored)
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)Result.Success();
        }

        var valueType = typeof(TResponse).GetGenericArguments()[0];
        var value = stored.ResponseBody is null ? null : JsonSerializer.Deserialize(stored.ResponseBody, valueType);
        var method = typeof(Result).GetMethod(nameof(Result.Success), 1, [Type.MakeGenericMethodParameter(0)])!.MakeGenericMethod(valueType);
        return (TResponse)method.Invoke(null, [value])!;
    }
}
