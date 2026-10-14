using System.ComponentModel.DataAnnotations;

namespace Taskify.Shared.Requests;

/// <summary>Only the Comment's Author, named by the identity, may edit it.</summary>
public record EditCommentRequest([Required, StringLength(10000, MinimumLength = 1)] string Text);
