using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SessionsApi.Models;

namespace SessionsApi.Services;

public interface ISupabaseRestService
{
    Task<IEnumerable<UserTab>> GetUserTabsAsync(string userId, string accessToken);
    Task<UserTab?> UpsertTabAsync(string userId, UpsertTabRequest request, string accessToken);
}

public class SupabaseRestService : ISupabaseRestService
{
    private readonly HttpClient _httpClient;
    private readonly string _supabaseUrl;
    private readonly string _supabaseKey;

    public SupabaseRestService(IConfiguration configuration)
    {
        _httpClient = new HttpClient();
        _supabaseUrl = configuration["Supabase:Url"] ?? throw new InvalidOperationException("Supabase URL not configured");
        _supabaseKey = configuration["Supabase:Key"] ?? throw new InvalidOperationException("Supabase Key not configured");
    }

    public async Task<IEnumerable<UserTab>> GetUserTabsAsync(string userId, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, 
            $"{_supabaseUrl}/rest/v1/user_tabs?user_id=eq.{userId}&order=device_id,last_seen.desc");
        
        SetHeaders(request, accessToken);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<List<UserTab>>(content, options) ?? new List<UserTab>();
    }

    public async Task<UserTab?> UpsertTabAsync(string userId, UpsertTabRequest request, string accessToken)
    {
        var body = new
        {
            user_id = userId,
            device_id = request.DeviceId,
            tab_id = request.TabId,
            user_agent = request.UserAgent,
            is_active = request.IsActive,
            last_seen = DateTime.UtcNow.ToString("o")
        };

        var json = JsonSerializer.Serialize(body);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_supabaseUrl}/rest/v1/user_tabs")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        SetHeaders(httpRequest, accessToken);
        httpRequest.Headers.Add("Prefer", "resolution=merge-duplicates,return=representation");

        var response = await _httpClient.SendAsync(httpRequest);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Supabase error: {error}");
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var tabs = JsonSerializer.Deserialize<List<UserTab>>(content, options);
        return tabs?.FirstOrDefault();
    }

    private void SetHeaders(HttpRequestMessage request, string accessToken)
    {
        request.Headers.Add("apikey", _supabaseKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }
}
