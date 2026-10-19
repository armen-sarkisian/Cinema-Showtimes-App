using Microsoft.AspNetCore.Mvc;

namespace CinemaShowtimes.Api.Errors;

public static class ApiProblemFactory
{
    public static ProblemDetails Create(
        HttpContext context,
        int status,
        string title,
        string detail,
        string code)
    {
        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{status}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        return problem;
    }
}
