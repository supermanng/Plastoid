using MediatR;
using Plastoids.SharedKernel.Results;

namespace Plastoids.SharedKernel.Cqrs;

/// <summary>A write. Runs inside one MySQL transaction (UnitOfWorkBehaviour) and is audited.</summary>
public interface ICommand : IRequest<Result>, IBaseCommand
{
}

public interface ICommand<TResponse> : IRequest<Result<TResponse>>, IBaseCommand
{
}

public interface IBaseCommand
{
}

/// <summary>A read. No transaction, no outbox, no audit.</summary>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}

/// <summary>
/// Commands that move money or record decisions must carry an Idempotency-Key (blueprint §17).
/// The endpoint copies the header into <see cref="IdempotencyKey"/>; the behaviour stores/replays the response.
/// </summary>
public interface IIdempotentCommand : IBaseCommand
{
    string IdempotencyKey { get; }
}

public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand
{
}

public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>
{
}

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>
{
}
