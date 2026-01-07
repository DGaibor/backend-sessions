using Npgsql;

namespace SessionsApi.Services;

public interface ISupabaseService
{
    NpgsqlConnection CreateConnection();
}

public class SupabaseService : ISupabaseService
{
    private readonly string _connectionString;

    public SupabaseService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Supabase") 
            ?? throw new InvalidOperationException("Supabase connection string not configured");
    }

    public NpgsqlConnection CreateConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }
}
