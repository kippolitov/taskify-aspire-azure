using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Taskify.Api.Hubs;
using Taskify.Api.Services;
using Taskify.Api.Tests.Helpers;
using Taskify.Shared.Enums;

namespace Taskify.Api.Tests.Hubs;

/// <summary>T025b / T042 — TaskifyHub unit tests and TaskService broadcast tests.</summary>
public class TaskifyHubTests
{
    // ── Hub method tests ─────────────────────────────────────────────────

    [Fact]
    public async Task JoinBoard_AddsConnectionToCorrectGroup()
    {
        var hub = new TaskifyHub();
        var groupManager = new RecordingGroupManager();
        hub.Groups = groupManager;
        hub.Context = new TestHubCallerContext("conn-abc");

        await hub.JoinBoard(42);

        Assert.Single(
            groupManager.Added,
            x => x.ConnectionId == "conn-abc" && x.GroupName == "board-42"
        );
    }

    [Fact]
    public async Task LeaveBoard_RemovesConnectionFromGroup()
    {
        var hub = new TaskifyHub();
        var groupManager = new RecordingGroupManager();
        hub.Groups = groupManager;
        hub.Context = new TestHubCallerContext("conn-xyz");

        await hub.LeaveBoard(7);

        Assert.Single(
            groupManager.Removed,
            x => x.ConnectionId == "conn-xyz" && x.GroupName == "board-7"
        );
    }

    [Fact]
    public async Task JoinBoard_GroupNameIncludesProjectId()
    {
        var hub = new TaskifyHub();
        var groupManager = new RecordingGroupManager();
        hub.Groups = groupManager;
        hub.Context = new TestHubCallerContext("c1");

        await hub.JoinBoard(99);

        Assert.Equal("board-99", groupManager.Added[0].GroupName);
    }

    [Fact]
    public async Task JoinThenLeaveBoard_RecordsExactlyOneAddAndOneRemove()
    {
        var hub = new TaskifyHub();
        var groupManager = new RecordingGroupManager();
        hub.Groups = groupManager;
        hub.Context = new TestHubCallerContext("c1");

        await hub.JoinBoard(1);
        await hub.LeaveBoard(1);

        Assert.Single(groupManager.Added);
        Assert.Single(groupManager.Removed);
    }

    // ── TaskService broadcast tests ──────────────────────────────────────

    [Fact]
    public async Task MoveTaskAsync_BroadcastsTaskMovedToGroup()
    {
        var db = TestDbFactory.CreateWithSeed(nameof(MoveTaskAsync_BroadcastsTaskMovedToGroup));
        var recordingHub = new RecordingHubContext<TaskifyHub>();
        var service = new TaskService(db, recordingHub);

        var dto = await service.MoveTaskAsync(1, ColumnStatus.Done);

        Assert.NotNull(dto);
        Assert.Equal(ColumnStatus.Done, dto!.Status);
        // Verify broadcast was sent to the correct board group
        Assert.Contains(
            recordingHub.SentMessages,
            m => m.GroupName == "board-1" && m.Method == "TaskMoved"
        );
    }
}

// ── Test doubles ─────────────────────────────────────────────────────────────

/// <summary>Records Group.AddToGroupAsync / RemoveFromGroupAsync calls.</summary>
internal sealed class RecordingGroupManager : IGroupManager
{
    public record GroupEntry(string ConnectionId, string GroupName);

    public readonly List<GroupEntry> Added = [];
    public readonly List<GroupEntry> Removed = [];

    public Task AddToGroupAsync(
        string connectionId,
        string groupName,
        CancellationToken cancellationToken = default
    )
    {
        Added.Add(new(connectionId, groupName));
        return Task.CompletedTask;
    }

    public Task RemoveFromGroupAsync(
        string connectionId,
        string groupName,
        CancellationToken cancellationToken = default
    )
    {
        Removed.Add(new(connectionId, groupName));
        return Task.CompletedTask;
    }
}

/// <summary>Minimal HubCallerContext with a fixed ConnectionId.</summary>
internal sealed class TestHubCallerContext : HubCallerContext
{
    public TestHubCallerContext(string connectionId) => ConnectionId = connectionId;

    public override string ConnectionId { get; }

    public override string? UserIdentifier => null;

    public override ClaimsPrincipal? User => null;

    public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

    public override IFeatureCollection Features { get; } = new FeatureCollection();

    public override CancellationToken ConnectionAborted => CancellationToken.None;

    public override void Abort() { }
}

/// <summary>Records calls to Clients.Group(...).SendCoreAsync(...).</summary>
internal sealed class RecordingHubContext<THub> : IHubContext<THub>
    where THub : Hub
{
    public record MessageEntry(string GroupName, string Method);

    public readonly List<MessageEntry> SentMessages = [];

    public IHubClients Clients => new RecordingHubClients(SentMessages);

    public IGroupManager Groups { get; } = new NullGroupManager();

    private sealed class RecordingHubClients(List<MessageEntry> log) : IHubClients
    {
        private IClientProxy GroupProxy(string name) => new RecordingClientProxy(name, log);

        public IClientProxy All => GroupProxy("*all*");

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) =>
            GroupProxy("*all*");

        public IClientProxy Client(string connectionId) => GroupProxy(connectionId);

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => GroupProxy("*clients*");

        public IClientProxy Group(string groupName) => GroupProxy(groupName);

        public IClientProxy GroupExcept(
            string groupName,
            IReadOnlyList<string> excludedConnectionIds
        ) => GroupProxy(groupName);

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => GroupProxy("*groups*");

        public IClientProxy User(string userId) => GroupProxy(userId);

        public IClientProxy Users(IReadOnlyList<string> userIds) => GroupProxy("*users*");
    }

    private sealed class RecordingClientProxy(string groupName, List<MessageEntry> log)
        : IClientProxy
    {
        public Task SendCoreAsync(
            string method,
            object?[] args,
            CancellationToken cancellationToken = default
        )
        {
            log.Add(new(groupName, method));
            return Task.CompletedTask;
        }
    }

    private sealed class NullGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;
    }
}
