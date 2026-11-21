using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Modules;
using Plastoids.Infrastructure.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Infrastructure.Behaviours;

/// <summary>
/// Opens one MySQL transaction on the command's module context, runs the handler, commits on success
/// (flushing outbox rows), rolls back on failure or exception. Queries skip this entirely.
/// </summary>
public sealed class UnitOfWorkBehaviour<TRequest, TResponse>(IServiceProvider serviceProvider, ModuleRegistry registry, IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseCommand
    where TResponse : Result
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            // Nested command (e.g. a consumer dispatching a command): join the ambient transaction.
            return await next(cancellationToken);
        }

        var module = registry.ForAssembly(typeof(TRequest).Assembly);
        var db = (ModuleDbContext)serviceProvider.GetRequiredService(module.DbContextType);
        await unitOfWork.BeginAsync(db, cancellationToken);
        try
        {
            var response = await next(cancellationToken);
            if (response.IsSuccess)
            {
                await unitOfWork.CommitAsync(cancellationToken);
            }
            else
            {
                await unitOfWork.RollbackAsync(cancellationToken);
            }

            return response;
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
