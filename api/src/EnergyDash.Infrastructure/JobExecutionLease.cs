using Microsoft.Data.SqlClient;

namespace EnergyDash.Infrastructure;

// A separate connection keeps this session lock alive across pricing transactions/tracker resets.
// IIS can overlap old/new processes during recycling; Quartz's in-memory JobKey lock is process-local.
public sealed class JobExecutionLease : IAsyncDisposable
{
    private readonly SqlConnection connection;
    private readonly string resource;
    private JobExecutionLease(SqlConnection connection, string resource) { this.connection = connection; this.resource = resource; }

    public static async Task<JobExecutionLease?> TryAcquireAsync(string connectionString, string jobGroup, CancellationToken ct)
    {
        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(ct);
            var resource = "EnergyDash.Job." + jobGroup;
            await using var command = connection.CreateCommand();
            command.CommandText = "DECLARE @result int; EXEC @result = sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0; SELECT @result;";
            command.Parameters.AddWithValue("@resource", resource);
            var result = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
            if (result >= 0) return new JobExecutionLease(connection, resource);
            if (result != -1) throw new InvalidOperationException("Scheduler execution lock unavailable.");
            await connection.DisposeAsync();
            return null;
        }
        catch { await connection.DisposeAsync(); throw; }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC sp_releaseapplock @Resource=@resource, @LockOwner='Session';";
            command.Parameters.AddWithValue("@resource", resource);
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }
        finally { await connection.DisposeAsync(); }
    }
}
