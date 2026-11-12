using System.Diagnostics;
using Herensuge.Common.Application.Messaging;
using Herensuge.Common.Domain;
using Microsoft.Extensions.Logging;

namespace Herensuge.Common.Application.Behaviors;

// Never logs the request payload: commands carry passwords, codes and tokens.
internal static class LoggingDecorator
{
    private const string Category = "Herensuge.Requests";

    internal sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> innerHandler,
        ILoggerFactory loggerFactory)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken) =>
            ExecuteAsync(loggerFactory, typeof(TCommand).Name, LogLevel.Information, () => innerHandler.Handle(command, cancellationToken));
    }

    internal sealed class CommandBaseHandler<TCommand>(
        ICommandHandler<TCommand> innerHandler,
        ILoggerFactory loggerFactory)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public Task<Result> Handle(TCommand command, CancellationToken cancellationToken) =>
            ExecuteAsync(loggerFactory, typeof(TCommand).Name, LogLevel.Information, () => innerHandler.Handle(command, cancellationToken));
    }

    internal sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> innerHandler,
        ILoggerFactory loggerFactory)
        : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken) =>
            ExecuteAsync(loggerFactory, typeof(TQuery).Name, LogLevel.Debug, () => innerHandler.Handle(query, cancellationToken));
    }

    private static async Task<TResult> ExecuteAsync<TResult>(
        ILoggerFactory loggerFactory,
        string requestName,
        LogLevel successLevel,
        Func<Task<TResult>> next)
        where TResult : Result
    {
        ILogger logger = loggerFactory.CreateLogger(Category);
        long startedAt = Stopwatch.GetTimestamp();

        try
        {
            TResult result = await next();
            double elapsed = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

            if (result.IsSuccess)
            {
                logger.Log(successLevel, "{RequestName} succeeded in {ElapsedMilliseconds:0.0} ms", requestName, elapsed);
            }
            else
            {
                logger.LogInformation(
                    "{RequestName} failed with {ErrorCode} in {ElapsedMilliseconds:0.0} ms",
                    requestName,
                    result.Error.Code,
                    elapsed);
            }

            return result;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "{RequestName} threw an unhandled exception", requestName);
            throw;
        }
    }
}
