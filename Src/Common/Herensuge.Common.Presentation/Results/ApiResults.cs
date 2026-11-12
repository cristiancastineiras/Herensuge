using System.Text.Json;
using Herensuge.Common.Domain;
using Microsoft.AspNetCore.Http;

namespace Herensuge.Common.Presentation.Results;

public static class ApiResults
{
    /// <summary>RFC 9457 problem details. The "code" extension is the stable key the SPA translates.</summary>
    public static IResult Problem(Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("A successful result cannot be converted into a problem.");
        }

        return Problem(result.Error);
    }

    public static IResult Problem(Error error)
    {
        int status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status400BadRequest,
        };

        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };
        if (error is ValidationError validation)
        {
            extensions["errors"] = validation.Errors
                .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.Code))
                .ToDictionary(group => group.Key, group => group.Select(e => e.Description).ToArray());
        }

        return Microsoft.AspNetCore.Http.Results.Problem(
            title: error.Type == ErrorType.Validation ? "One or more validation errors occurred." : error.Description,
            statusCode: status,
            extensions: extensions);
    }
}

public static class ResultExtensions
{
    public static IResult Match(this Result result, Func<IResult> onSuccess) =>
        result.IsSuccess ? onSuccess() : ApiResults.Problem(result);

    public static IResult Match<TValue>(this Result<TValue> result, Func<TValue, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : ApiResults.Problem(result);
}
