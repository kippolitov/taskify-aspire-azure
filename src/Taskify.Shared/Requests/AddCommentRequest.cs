using System.ComponentModel.DataAnnotations;

namespace Taskify.Shared.Requests;

/// <summary>The Author is the User named by the identity, never a field of the request.</summary>
public record AddCommentRequest([Required, StringLength(10000, MinimumLength = 1)] string Text);
