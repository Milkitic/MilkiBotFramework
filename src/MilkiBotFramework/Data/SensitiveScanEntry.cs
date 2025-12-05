using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace MilkiBotFramework.Data;

[Index(nameof(Source), IsUnique = true)]
public record SensitiveScanEntry
{
    [Key]
    public int Id { get; set; }

    [MaxLength(ushort.MaxValue)]
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [MaxLength(ushort.MaxValue)]
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("review_result_code")]
    public int ReviewResultCode { get; set; }

    [MaxLength(32)]
    [JsonPropertyName("violation_type")]
    public string? ViolationType { get; set; }

    [MaxLength(32)]
    [JsonPropertyName("suggestion")]
    public string? Suggestion { get; set; }

    [MaxLength(256)]
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [MaxLength(512)]
    [JsonPropertyName("words")]
    public List<string> Words { get; set; } = new();
}