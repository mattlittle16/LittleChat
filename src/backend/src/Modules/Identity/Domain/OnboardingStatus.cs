using System.Text.Json.Serialization;

namespace Identity.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OnboardingStatus
{
    [JsonPropertyName("not_started")] NotStarted,
    [JsonPropertyName("remind_later")] RemindLater,
    [JsonPropertyName("dismissed")]    Dismissed,
}
