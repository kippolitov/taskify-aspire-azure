using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Taskify.Shared.Requests;

/// <summary>Must name an existing User; the server returns 404 otherwise.</summary>
public record TokenRequest([property: Required, JsonRequired] int UserId);
