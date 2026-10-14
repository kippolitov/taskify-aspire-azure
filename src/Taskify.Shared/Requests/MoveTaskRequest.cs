using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Taskify.Shared.Enums;

namespace Taskify.Shared.Requests;

/// <summary>
/// An unknown Status name fails JSON binding. An undefined number, such as 7, which the
/// string-enum converter accepts, is rejected by <see cref="EnumDataTypeAttribute"/>.
/// </summary>
public record MoveTaskRequest(
    [property: Required, JsonRequired, EnumDataType(typeof(ColumnStatus))] ColumnStatus Status
);
