using FluentValidation;
using FluentValidation.Results;
using Herensuge.Common.Application.Messaging;
using Herensuge.Common.Domain;

namespace Herensuge.Common.Application.Behaviors;

internal static class ValidationDecorator
{
    internal sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> innerHandler,
        IEnumerable<IValidator<TCommand>> validators)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
        {
            ValidationError? error = await ValidateAsync(command, validators, cancellationToken);
            return error is null
                ? await innerHandler.Handle(command, cancellationToken)
                : Result.Failure<TResponse>(error);
        }
    }

    internal sealed class CommandBaseHandler<TCommand>(
        ICommandHandler<TCommand> innerHandler,
        IEnumerable<IValidator<TCommand>> validators)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task<Result> Handle(TCommand command, CancellationToken cancellationToken)
        {
            ValidationError? error = await ValidateAsync(command, validators, cancellationToken);
            return error is null
                ? await innerHandler.Handle(command, cancellationToken)
                : Result.Failure(error);
        }
    }

    internal sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> innerHandler,
        IEnumerable<IValidator<TQuery>> validators)
        : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
        {
            ValidationError? error = await ValidateAsync(query, validators, cancellationToken);
            return error is null
                ? await innerHandler.Handle(query, cancellationToken)
                : Result.Failure<TResponse>(error);
        }
    }

    private static async Task<ValidationError?> ValidateAsync<TRequest>(
        TRequest request,
        IEnumerable<IValidator<TRequest>> validators,
        CancellationToken cancellationToken)
    {
        IValidator<TRequest>[] all = [.. validators];
        if (all.Length == 0)
        {
            return null;
        }

        var context = new ValidationContext<TRequest>(request);
        ValidationResult[] results = await Task.WhenAll(all.Select(v => v.ValidateAsync(context, cancellationToken)));

        Error[] errors =
        [
            .. results
                .SelectMany(result => result.Errors)
                .Select(failure => Error.Validation(failure.PropertyName, failure.ErrorMessage)),
        ];

        return errors.Length == 0 ? null : new ValidationError(errors);
    }
}
