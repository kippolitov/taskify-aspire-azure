using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Taskify.Api.Controllers;
using Taskify.Api.Data;
using Taskify.Api.Services;
using Taskify.Api.Tests.Helpers;
using Taskify.Shared.Dtos;

namespace Taskify.Api.Tests.Controllers;

/// <summary>T034a — CommentsController integration-style unit tests.</summary>
public class CommentsControllerTests
{
    private static (CommentsController ctrl, TaskifyDbContext db) Build(string name)
    {
        var db = TestDbFactory.CreateWithSeed(name);
        var hub = new NullHubContext<Taskify.Api.Hubs.TaskifyHub>();
        var service = new CommentService(db, hub);
        var ctrl = new CommentsController(db, service);
        return (ctrl, db);
    }

    [Fact]
    public async Task GetTaskComments_ExistingTask_ReturnsOkWithComments()
    {
        var (ctrl, _) = Build(nameof(GetTaskComments_ExistingTask_ReturnsOkWithComments));

        var result = await ctrl.GetTaskComments(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var comments = Assert.IsAssignableFrom<IEnumerable<CommentDto>>(ok.Value);
        Assert.Single(comments);
    }

    [Fact]
    public async Task GetTaskComments_NonExistingTask_Returns404()
    {
        var (ctrl, _) = Build(nameof(GetTaskComments_NonExistingTask_Returns404));

        var result = await ctrl.GetTaskComments(999);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public async Task AddComment_ValidRequest_Returns201AndPersists()
    {
        var (ctrl, db) = Build(nameof(AddComment_ValidRequest_Returns201AndPersists));
        var request = new AddCommentRequest(1, "A new comment");

        var result = await ctrl.AddComment(1, request);

        var created = Assert.IsType<CreatedResult>(result);
        var dto = Assert.IsType<CommentDto>(created.Value);
        Assert.Equal("A new comment", dto.Text);
        Assert.Equal(1, dto.Author.Id);

        var count = db.Comments.Count(c => c.TaskItemId == 1);
        Assert.Equal(2, count); // 1 seeded + 1 new
    }

    [Fact]
    public async Task AddComment_NonExistingTask_Returns404()
    {
        var (ctrl, _) = Build(nameof(AddComment_NonExistingTask_Returns404));
        var request = new AddCommentRequest(1, "Text");

        var result = await ctrl.AddComment(999, request);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public async Task AddComment_NonExistingAuthor_Returns422()
    {
        var (ctrl, _) = Build(nameof(AddComment_NonExistingAuthor_Returns422));
        var request = new AddCommentRequest(999, "Text");

        var result = await ctrl.AddComment(1, request);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, problem.StatusCode);
    }

    [Fact]
    public async Task EditComment_ByOwner_ReturnsOkWithUpdatedText()
    {
        var (ctrl, db) = Build(nameof(EditComment_ByOwner_ReturnsOkWithUpdatedText));
        var request = new EditCommentRequest(1, "Edited text");

        var result = await ctrl.EditComment(1, request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<CommentDto>(ok.Value);
        Assert.Equal("Edited text", dto.Text);
        Assert.NotNull(dto.EditedAt);

        var persisted = await db.Comments.FindAsync(1);
        Assert.Equal("Edited text", persisted!.Text);
    }

    [Fact]
    public async Task EditComment_ByNonOwner_Returns403()
    {
        // Seeded comment 1 is authored by user 1; user 2 should be forbidden
        var (ctrl, _) = Build(nameof(EditComment_ByNonOwner_Returns403));
        var request = new EditCommentRequest(2, "Hacked edit");

        var result = await ctrl.EditComment(1, request);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task DeleteComment_ByOwner_Returns204AndRemovesComment()
    {
        var (ctrl, db) = Build(nameof(DeleteComment_ByOwner_Returns204AndRemovesComment));

        var result = await ctrl.DeleteComment(1, 1);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await db.Comments.FindAsync(1));
    }

    [Fact]
    public async Task DeleteComment_ByNonOwner_Returns403()
    {
        var (ctrl, _) = Build(nameof(DeleteComment_ByNonOwner_Returns403));

        var result = await ctrl.DeleteComment(1, 2);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task DeleteComment_NonExistingComment_Returns404()
    {
        var (ctrl, _) = Build(nameof(DeleteComment_NonExistingComment_Returns404));

        var result = await ctrl.DeleteComment(999, 1);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }
}
