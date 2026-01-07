# Backend - Session Tracker API

ASP.NET Core 8 Minimal API for tab presence tracking.

## Setup Instructions

### Prerequisites

- Docker

### Installation

```bash
docker compose -f docker-compose.yml build
docker compose -f docker-compose.yml up -d
```

The API will be available at `http://localhost:5001`.

### Verify

```bash
curl http://localhost:5001/health
```

### Configuration

Settings are in `appsettings.json`:

```json
{
  "Supabase": {
    "Url": "https://your-project.supabase.co",
    "Key": "your-anon-key"
  }
}
```

## API Endpoints

### GET /api/tabs

Returns all tabs for the authenticated user grouped by device.

**Headers:**
- `Authorization: Bearer <access_token>`
- `X-User-Id: <user_uuid>`

**Response:**
```json
{
  "onlineCount": 2,
  "devices": [
    {
      "deviceId": "abc123...",
      "tabs": [
        {
          "deviceId": "abc123...",
          "tabId": "xyz789...",
          "userAgent": "Mozilla/5.0...",
          "isActive": true,
          "lastSeen": "2024-01-01T12:00:00Z",
          "status": "active"
        }
      ]
    }
  ]
}
```

### POST /api/tabs

Upserts a tab's presence state (heartbeat).

**Headers:**
- `Authorization: Bearer <access_token>`
- `X-User-Id: <user_uuid>`

**Body:**
```json
{
  "deviceId": "abc123...",
  "tabId": "xyz789...",
  "userAgent": "Mozilla/5.0...",
  "isActive": true
}
```

### GET /health

Health check endpoint.

## Authentication

The backend acts as a proxy to Supabase REST API. It forwards the user's access token to Supabase for Row Level Security (RLS) enforcement.

**Flow:**
1. Frontend authenticates with Supabase Auth
2. Frontend sends requests with `Authorization: Bearer <token>` and `X-User-Id` headers
3. Backend forwards token to Supabase REST API
4. Supabase validates token and applies RLS policies

## Presence Strategy

### Status Calculation

Status is calculated based on `lastSeen` timestamp:

```csharp
var secondsAgo = (DateTime.UtcNow - tab.LastSeen).TotalSeconds;
var status = secondsAgo switch {
    < 30 => "active",
    < 60 => "idle",
    _ => "stale"
};
```

### Why Tri-state?

1. **No `beforeunload` reliance** - Browser events are unreliable
2. **Graceful degradation** - Tabs "fade out" instead of flickering
3. **Throttling tolerance** - Background tabs have reduced timer precision

## Trade-offs and Limitations

Due to time constraints:

- **Proxy architecture** - Backend proxies to Supabase REST API instead of direct PostgreSQL (simpler auth handling)
- **No caching** - Status calculated on every request
- **No cleanup job** - Stale entries remain in database
- **Minimal validation** - Basic input validation only
- **No rate limiting** - Could be added for production

## Architecture

```
backend-sessions/
├── Endpoints/
│   └── TabEndpoints.cs       # GET/POST /api/tabs
├── Models/
│   └── UserTab.cs            # Domain entity
├── Services/
│   └── SupabaseRestService.cs # Supabase REST client
├── Program.cs                # Entry point + DI
└── Dockerfile                # Container config
```

**Tech Stack**: ASP.NET Core 8, Minimal API, Docker

## Database Schema

```sql
create table public.user_tabs (
  user_id uuid not null references auth.users(id) on delete cascade,
  device_id text not null,
  tab_id text not null,
  user_agent text not null,
  is_active boolean not null default false,
  last_seen timestamptz not null default now(),
  created_at timestamptz not null default now(),
  primary key (user_id, device_id, tab_id)
);
```
