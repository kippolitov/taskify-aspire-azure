using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Taskify.Shared.Dtos;
using Taskify.Shared.Enums;
using Taskify.Web.Components.Shared;
using Taskify.Web.Services;
using Taskify.Web.Tests.Helpers;

namespace Taskify.Web.Tests.Components;

/// <summary>T034b — CommentItem.razor bUnit tests.</summary>
public class CommentItemTests : BunitContext
{
    private static readonly UserDto Alice = TestData.FiveUsers[0]; // id=1
    private static readonly UserDto Bob = TestData.FiveUsers[1]; // id=2

    private static CommentDto MakeComment(
        UserDto author,
        string text = "Great progress!",
        DateTimeOffset? editedAt = null
    ) =>
        new(
            Id: 10,
            TaskId: 1,
            Author: author,
            Text: text,
            CreatedAt: new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero),
            EditedAt: editedAt
        );

    private void RegisterApiClient()
    {
        var handler = new TestApiHandler(new Dictionary<string, string>());
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        Services.AddScoped(_ => new ApiClient(httpClient));
    }

    [Fact]
    public void CommentItem_RendersCommentText()
    {
        RegisterApiClient();

        var comment = MakeComment(Alice, "Looks good to me!");
        var cut = Render<CommentItem>(p =>
        {
            p.Add(c => c.Comment, comment);
            p.Add(c => c.CurrentUserId, null);
        });

        Assert.Contains("Looks good to me!", cut.Markup);
    }

    [Fact]
    public void CommentItem_RendersAuthorName()
    {
        RegisterApiClient();

        var comment = MakeComment(Alice);
        var cut = Render<CommentItem>(p =>
        {
            p.Add(c => c.Comment, comment);
            p.Add(c => c.CurrentUserId, null);
        });

        Assert.Contains("Alice Chen", cut.Markup);
    }

    [Fact]
    public void CommentItem_WhenCurrentUserIsAuthor_ShowsEditAndDeleteControls()
    {
        RegisterApiClient();

        var comment = MakeComment(Alice);
        var cut = Render<CommentItem>(p =>
        {
            p.Add(c => c.Comment, comment);
            p.Add(c => c.CurrentUserId, Alice.Id); // Alice is author AND current user
        });

        Assert.NotNull(cut.Find("[aria-label='Edit comment']"));
        Assert.NotNull(cut.Find("[aria-label='Delete comment']"));
    }

    [Fact]
    public void CommentItem_WhenCurrentUserIsNotAuthor_HidesEditAndDeleteControls()
    {
        RegisterApiClient();

        var comment = MakeComment(Alice);
        var cut = Render<CommentItem>(p =>
        {
            p.Add(c => c.Comment, comment);
            p.Add(c => c.CurrentUserId, Bob.Id); // Bob is NOT the author
        });

        Assert.Empty(cut.FindAll("[aria-label='Edit comment']"));
        Assert.Empty(cut.FindAll("[aria-label='Delete comment']"));
    }

    [Fact]
    public void CommentItem_WhenNoCurrentUser_HidesEditAndDeleteControls()
    {
        RegisterApiClient();

        var comment = MakeComment(Alice);
        var cut = Render<CommentItem>(p =>
        {
            p.Add(c => c.Comment, comment);
            p.Add(c => c.CurrentUserId, null);
        });

        Assert.Empty(cut.FindAll("[aria-label='Edit comment']"));
        Assert.Empty(cut.FindAll("[aria-label='Delete comment']"));
    }

    [Fact]
    public void CommentItem_WhenEditedAtIsSet_ShowsEditedBadge()
    {
        RegisterApiClient();

        var editedAt = new DateTimeOffset(2026, 3, 6, 10, 0, 0, TimeSpan.Zero);
        var comment = MakeComment(Alice, editedAt: editedAt);
        var cut = Render<CommentItem>(p =>
        {
            p.Add(c => c.Comment, comment);
            p.Add(c => c.CurrentUserId, null);
        });

        Assert.Contains("edited", cut.Markup);
    }
}
