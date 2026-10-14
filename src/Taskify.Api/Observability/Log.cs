namespace Taskify.Api.Observability;

internal static partial class Log
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Broadcasting {Message} to the board of project {ProjectId} failed; the change is saved."
    )]
    public static partial void BroadcastFailed(
        ILogger logger,
        string message,
        int projectId,
        Exception exception
    );

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Unexpected error while handling the request."
    )]
    public static partial void UnexpectedError(ILogger logger, Exception exception);
}
