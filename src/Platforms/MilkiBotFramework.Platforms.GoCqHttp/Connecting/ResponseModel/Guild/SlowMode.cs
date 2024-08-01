#nullable disable

using System.Text.Json.Serialization;

namespace MilkiBotFramework.Platforms.GoCqHttp.Connecting.ResponseModel.Guild;

public class SlowMode
{
    [JsonPropertyName("slow_mode_circle")]
    public long SlowModeCircle { get; set; }

    [JsonPropertyName("slow_mode_key")]
    public long SlowModeKey { get; set; }

    [JsonPropertyName("slow_mode_text")]
    public string SlowModeText { get; set; }

    [JsonPropertyName("speak_frequency")]
    public long SpeakFrequency { get; set; }
}