using SessionsApi.Models;
using SessionsApi.Services;

namespace SessionsApi.Endpoints;

public static class TabEndpoints
{
    private const int ActiveThresholdSeconds = 30;
    private const int IdleThresholdSeconds = 60;

    public static void MapTabEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tabs");

        group.MapGet("/", GetUserTabs);
        group.MapPost("/", UpsertTab);
    }

    private static async Task<IResult> GetUserTabs(
        HttpContext context,
        ISupabaseRestService supabaseService)
    {
        var (userId, accessToken) = GetAuthFromHeaders(context);
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(accessToken))
            return Results.Unauthorized();

        var tabs = await supabaseService.GetUserTabsAsync(userId, accessToken);
        
        var tabResponses = tabs.Select(MapToResponse).ToList();
        
        var grouped = tabResponses
            .GroupBy(t => t.DeviceId)
            .Select(g => new
            {
                DeviceId = g.Key,
                Tabs = g.ToList()
            });

        var onlineCount = tabResponses.Count(t => t.Status == "active" || t.Status == "idle");

        return Results.Ok(new
        {
            OnlineCount = onlineCount,
            Devices = grouped
        });
    }

    private static async Task<IResult> UpsertTab(
        HttpContext context,
        UpsertTabRequest request,
        ISupabaseRestService supabaseService)
    {
        var (userId, accessToken) = GetAuthFromHeaders(context);
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(accessToken))
            return Results.Unauthorized();

        if (string.IsNullOrEmpty(request.DeviceId) || string.IsNullOrEmpty(request.TabId))
            return Results.BadRequest("DeviceId and TabId are required");

        var tab = await supabaseService.UpsertTabAsync(userId, request, accessToken);
        return tab != null ? Results.Ok(MapToResponse(tab)) : Results.Problem("Failed to upsert tab");
    }

    private static (string? userId, string? accessToken) GetAuthFromHeaders(HttpContext context)
    {
        var userId = context.Request.Headers["X-User-Id"].FirstOrDefault();
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        var accessToken = authHeader?.Replace("Bearer ", "");
        return (userId, accessToken);
    }

    private static TabResponse MapToResponse(UserTab tab)
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
