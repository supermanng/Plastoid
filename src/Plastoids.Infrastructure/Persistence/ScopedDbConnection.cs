using MySqlConnector;

namespace Plastoids.Infrastructure.Persistence;

/// <summary>
/// One MySQL connection per DI scope, shared by every module DbContext in that scope, so a command can
/// write business rows, ledger postings and outbox rows in ONE transaction even across module contexts.
/// </summary>
public sealed class ScopedDbConnection : IAsyncDisposable, IDisposable
{
    private readonly Lazy<MySqlConnection> _connection;

    public ScopedDbConnection(string connectionString)
    {
        _connection = new Lazy<MySqlConnection>(() => new MySqlConnection(connectionString));
    }

    public MySqlConnection Connection => _connection.Value;

    public void Dispose()
    {
        if (_connection.IsValueCreated)
        {
            _connection.Value.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection.IsValueCreated)
        {
            await _connection.Value.DisposeAsync();
        }
    }
}
