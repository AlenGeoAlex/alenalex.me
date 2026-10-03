using AlenAlex.Api.Json;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AlenAlex.Api.Infrastructure.Http;

public sealed record ErrorResponse(string Error);

public static class ApiErrors
{
    public static JsonHttpResult<ErrorResponse> Create(int statusCode, string message) =>
        TypedResults.Json(new ErrorResponse(message), AppJson.Context.ErrorResponse, statusCode: statusCode);

    public static JsonHttpResult<ErrorResponse> BadRequest() => Create(StatusCodes.Status400BadRequest, "bad request");

    public static JsonHttpResult<ErrorResponse> Internal() => Create(StatusCodes.Status500InternalServerError, "internal error");
}
