using Microsoft.AspNetCore.Http.HttpResults;
using Taskify.Shared.Dtos;

namespace Taskify.Api.Endpoints;

public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/notifications")
            .WithTags("Notifications")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // A placeholder that always returns an empty list; out of scope.
        group.MapGet("", GetAll);

        return group;
    }

    private static Ok<NotificationDto[]> GetAll() =>
        TypedResults.Ok(Array.Empty<NotificationDto>());
}
