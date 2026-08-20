using System.Text.Json.Serialization;
using Domain.Entities.Base;
using Domain.Enum;

namespace Domain.Entities;

/// <summary>
/// An internal staff account. Kept apart from resident users on purpose: staff act across every site,
/// so they get their own partition, their own credentials and their own password hashing.
/// </summary>
public class StaffEntity : IEntity
{
    [JsonPropertyName("pk")] public string Pk => GetPk();

    [JsonPropertyName("sk")] public string Sk => Id;

    [JsonPropertyName("id")] public string Id { get; set; } = default!;

    [JsonPropertyName("email")] public string Email { get; set; } = default!;

    [JsonPropertyName("fullName")] public string FullName { get; set; } = default!;

    /// <summary>Base64 PBKDF2 hash. Never the password itself.</summary>
    [JsonPropertyName("passwordHash")] public string PasswordHash { get; set; } = default!;

    /// <summary>Base64 salt, unique per account.</summary>
    [JsonPropertyName("passwordSalt")] public string PasswordSalt { get; set; } = default!;

    [JsonPropertyName("roles")] public List<StaffRole> Roles { get; set; } = new();

    /// <summary>
    /// Base32 TOTP shared secret. Issued at provisioning time, never returned again after that.
    /// Protected at rest by the table's server side encryption.
    /// </summary>
    [JsonPropertyName("totpSecret")] public string TotpSecret { get; set; } = default!;

    [JsonPropertyName("isActive")] public bool IsActive { get; set; } = true;

    [JsonPropertyName("lastLoginAt")] public DateTime? LastLoginAt { get; set; }

    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static string GetPk() => "staff";
}
