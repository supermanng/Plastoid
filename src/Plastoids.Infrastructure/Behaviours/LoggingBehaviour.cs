using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Infrastructure.Behaviours;

public sealed class LoggingBehaviour<TRequest, TResponse>(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next(cancellationToken);
            if (response is Result { IsFailure: true } failed)
            {
                logger.LogWarning("{Request} failed with {Code} in {Elapsed}ms", name, failed.Error.Code, sw.ElapsedMilliseconds);
            }
            else
            {
                logger.LogInformation("{Request} handled in {Elapsed}ms", name, sw.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Request} threw after {Elapsed}ms", name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
