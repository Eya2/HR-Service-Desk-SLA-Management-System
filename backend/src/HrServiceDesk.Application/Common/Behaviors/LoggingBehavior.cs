using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrServiceDesk.Application.Common.Behaviors;

/// <summary>Logs the request name and duration. Request payloads are never logged, to keep personal data out of logs.</summary>
internal sealed partial class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var response = await next();
        LogHandled(logger, typeof(TRequest).Name, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return response;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {RequestName} in {ElapsedMs:0.0} ms")]
    private static partial void LogHandled(ILogger logger, string requestName, double elapsedMs);
}
