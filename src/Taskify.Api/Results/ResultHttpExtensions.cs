using Microsoft.AspNetCore.Http.HttpResults;

namespace Taskify.Api.Results;

/// <summary>The only place where an outcome becomes an HTTP response.</summary>
public static class ResultHttpExtensions
{
    public static Results<Ok<T>, ProblemHttpResult> ToHttpResult<T>(this Result<T> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();

    public static Results<Created<T>, ProblemHttpResult> ToCreatedResult<T>(
        this Result<T> result,
        Func<T, string> location
    ) =>
        result.IsSuccess
            ? TypedResults.Created(location(result.Value!), result.Value)
            : result.Error!.ToProblem();

    public static Results<NoContent, ProblemHttpResult> ToNoContentResult(this Result result) =>
        result.IsSuccess ? TypedResults.NoContent() : result.Error!.ToProblem();

    private static ProblemHttpResult ToProblem(this Error error) =>
        TypedResults.Problem(
            detail: error.Detail,
            statusCode: error.Kind switch
            {
                ErrorKind.NotFound => StatusCodes.Status404NotFound,
                ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status422UnprocessableEntity,
            }
        );
}
