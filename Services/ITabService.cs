using Dapper;
using SessionsApi.Models;

namespace SessionsApi.Services;

public interface ITabService
{
    Task<IEnumerable<TabResponse>> GetUserTabsAsync(Guid userId);
    Task<TabResponse?> UpsertTabAsync(Guid userId, UpsertTabRequest request);
    Task<bool> DeleteTabAsync(Guid userId, string deviceId, string tabId);
    Task<int> CleanupStaleTabs(int staleMinutes);
}

public class TabService : ITabService
{
    private readonly ISupabaseService _supabaseService;
    private const int ActiveThresholdSeconds = 30;
    private const int IdleThresholdSeconds = 60;

    public TabService(ISupabaseService supabaseService)
    {
        _supabaseService = supabaseService;
    }

    public async Task<IEnumerable<TabResponse>> GetUserTabsAsync(Guid userId)
    {
        using var connection = _supabaseService.CreateConnection();
        
        var sql = @"
            SELECT device_id as DeviceId, tab_id as TabId, user_agent as UserAgent, 
                   is_active as IsActive, last_seen as LastSeen
            FROM public.user_tabs 
            WHERE user_id = @UserId
            ORDER BY device_id, last_seen DESC";

        var tabs = await connection.QueryAsync<UserTab>(sql, new { UserId = userId });
        
        return tabs.Select(MapToResponse);
    }

    public async Task<TabResponse?> UpsertTabAsync(Guid userId, UpsertTabRequest request)
    {
        using var connection = _supabaseService.CreateConnection();
        
        var sql = @"
            INSERT INTO public.user_tabs (user_id, device_id, tab_id, user_agent, is_active, last_seen)
            VALUES (@UserId, @DeviceId, @TabId, @UserAgent, @IsActive, NOW())
            ON CONFLICT (user_id, device_id, tab_id) 
            DO UPDATE SET 
                is_active = @IsActive,
                last_seen = NOW(),
                user_agent = @UserAgent
            RETURNING device_id as DeviceId, tab_id as TabId, user_agent as UserAgent, 
                      is_active as IsActive, last_seen as LastSeen";

        var tab = await connection.QuerySingleOrDefaultAsync<UserTab>(sql, new
        {
            UserId = userId,
            request.DeviceId,
            request.TabId,
            request.UserAgent,
            request.IsActive
        });

        return tab != null ? MapToResponse(tab) : null;
    }

    public async Task<bool> DeleteTabAsync(Guid userId, string deviceId, string tabId)
    {
        using var connection = _supabaseService.CreateConnection();
        
        var sql = @"
            DELETE FROM public.user_tabs 
            WHERE user_id = @UserId AND device_id = @DeviceId AND tab_id = @TabId";

        var affected = await connection.ExecuteAsync(sql, new { UserId = userId, DeviceId = deviceId, TabId = tabId });
        return affected > 0;
    }

    public async Task<int> CleanupStaleTabs(int staleMinutes)
    {
        using var connection = _supabaseService.CreateConnection();
        
        var sql = @"
            DELETE FROM public.user_tabs 
            WHERE last_seen < NOW() - INTERVAL '@Minutes minutes'";

        return await connection.ExecuteAsync(sql.Replace("@Minutes", staleMinutes.ToString()));
    }

    private TabResponse MapToResponse(UserTab tab)
    {
        var secondsSinceLastSeen = (DateTime.UtcNow - tab.LastSeen).TotalSeconds;
        
        string status;
        if (secondsSinceLastSeen <= ActiveThresholdSeconds && tab.IsActive)
            status = "active";
        else if (secondsSinceLastSeen <= IdleThresholdSeconds)
            status = "idle";
        else
            status = "stale";

        return new TabResponse
        {
            DeviceId = tab.DeviceId,
            TabId = tab.TabId,
            UserAgent = tab.UserAgent,
            IsActive = tab.IsActive,
            LastSeen = tab.LastSeen,
            Status = status
        };
    }
}
