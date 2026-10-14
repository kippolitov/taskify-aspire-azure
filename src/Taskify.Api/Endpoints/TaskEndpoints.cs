using Microsoft.AspNetCore.Http.HttpResults;
using Taskify.Api.Results;
using Taskify.Api.Services;
using Taskify.Shared.Dtos;
using Taskify.Shared.Requests;

namespace Taskify.Api.Endpoints;

public static class TaskEndpoints
{
    public static RouteGroupBuilder MapTaskEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api")
            .WithTags("Tasks")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group
            .MapGet("/projects/{projectId:int}/tasks", GetProjectTasksAsync)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group
            .MapGet("/tasks/{id:int}", GetByIdAsync)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group
            .MapPost("/projects/{projectId:int}/tasks", CreateAsync)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        group
            .MapPut("/tasks/{id:int}", UpdateAsync)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group
            .MapPatch("/tasks/{id:int}/assignee", AssignAsync)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        group
            .MapPatch("/tasks/{id:int}/status", MoveAsync)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<
        Results<Ok<IReadOnlyList<TaskDto>>, ProblemHttpResult>
    > GetProjectTasksAsync(int projectId, TaskService tasks, CancellationToken ct) =>
        (await tasks.GetProjectTasksAsync(projectId, ct)).ToHttpResult();

    private static async Task<Results<Ok<TaskDto>, ProblemHttpResult>> GetByIdAsync(
        int id,
        TaskService tasks,
        CancellationToken ct
    ) => (await tasks.GetTaskAsync(id, ct)).ToHttpResult();

    private static async Task<Results<Created<TaskDto>, ProblemHttpResult>> CreateAsync(
        int projectId,
        CreateTaskRequest request,
        TaskService tasks,
        CancellationToken ct
    ) =>
        (await tasks.CreateTaskAsync(projectId, request, ct)).ToCreatedResult(t =>
            $"/api/tasks/{t.Id}"
        );

    private static async Task<Results<Ok<TaskDto>, ProblemHttpResult>> UpdateAsync(
        int id,
        UpdateTaskRequest request,
        TaskService tasks,
        CancellationToken ct
    ) => (await tasks.UpdateTaskAsync(id, request, ct)).ToHttpResult();

    private static async Task<Results<Ok<TaskDto>, ProblemHttpResult>> AssignAsync(
        int id,
        AssignTaskRequest request,
        TaskService tasks,
        CancellationToken ct
    ) => (await tasks.AssignTaskAsync(id, request, ct)).ToHttpResult();

    private static async Task<Results<Ok<TaskDto>, ProblemHttpResult>> MoveAsync(
        int id,
        MoveTaskRequest request,
        TaskService tasks,
        CancellationToken ct
    ) => (await tasks.MoveTaskAsync(id, request.Status, ct)).ToHttpResult();
}
