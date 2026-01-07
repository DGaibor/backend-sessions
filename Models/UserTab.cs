using System.Text.Json.Serialization;

namespace SessionsApi.Models;

public class UserTab
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;
    
    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; } = string.Empty;
    
    [JsonPropertyName("tab_id")]
    public string TabId { get; set; } = string.Empty;
    
    [JsonPropertyName("user_agent")]
    public string UserAgent { get; set; } = string.Empty;
    
    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
    
    [JsonPropertyName("last_seen")]
    public DateTime LastSeen { get; set; }
    
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

public class UpsertTabRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public string TabId { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class TabResponse
{
    public string DeviceId { get; set; } = string.Empty;
    public string TabId { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime LastSeen { get; set; }
    public string Status { get; set; } = string.Empty;
}
