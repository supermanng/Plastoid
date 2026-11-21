using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MySqlConnector;
using Testcontainers.Kafka;
using DotNet.Testcontainers.Containers;
using Testcontainers.MySql;
using Testcontainers.Redis;

namespace Plastoids.IntegrationTests;

/// <summary>
/// Hosts the API against real Kafka and Redis containers and a real MySQL 8.x.
/// MySQL comes from Testcontainers by default; set PLASTOIDS_TEST_MYSQL to a connection string with CREATE DATABASE
/// rights to use an existing server instead (a throw-away database "plastoids_test_&lt;id&gt;" is created and dropped).
/// </summary>
public sealed class PlastoidsApiFactory : WebApplicationFactory<Plastoids.Api.Program>, IAsyncLifetime
{
    public const string ExternalMySqlVariable = "PLASTOIDS_TEST_MYSQL";

    private readonly MySqlContainer? _mysql;
    private readonly string? _externalMySql;
    private string? _externalDatabase;

    private readonly KafkaContainer _kafka = new KafkaBuilder("confluentinc/cp-kafka:7.8.0").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();
    // S3-compatible store: MinIO's public images were withdrawn, so Adobe S3Mock stands in (see docker-compose.yml).
    private readonly IContainer _s3 = new ContainerBuilder("adobe/s3mock:4.9.0")
        .WithEnvironment("initialBuckets", "plastoids-test")
        .WithPortBinding(9090, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(9090).ForPath("/")))
        .Build();

    public PlastoidsApiFactory()
    {
        _externalMySql = Environment.GetEnvironmentVariable(ExternalMySqlVariable);
        if (string.IsNullOrWhiteSpace(_externalMySql))
        {
            _mysql = new MySqlBuilder("mysql:8.4")
                .WithDatabase("plastoids")
                .WithUsername("plastoids")
                .WithPassword("plastoids")
                // Start as the mysql user so the image entrypoint skips `gosu`, which hangs on some Docker Desktop
                // builds (see ADR 0006). The data dir is already owned by mysql inside the image.
                .WithCreateParameterModifier(p => p.User = "mysql")
                // The Audit module's append-only triggers need this when binary logging is on (MySQL 8 default).
                .WithCommand("--log-bin-trust-function-creators=1")
                // The module's default readiness probe runs `mysql --wait` with a root-owned 0600 my.cnf that the
                // mysql user cannot read, so it retries forever. Wait for the real server's log line instead
                // (the entrypoint's temporary server reports "port: 0").
                .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(@"ready for connections\. Version: .* port: 3306"))
                .Build();
        }
    }

    public string MySqlConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var mysqlTask = _mysql is not null ? _mysql.StartAsync() : PrepareExternalDatabaseAsync();
        await Task.WhenAll(mysqlTask, _kafka.StartAsync(), _redis.StartAsync(), _s3.StartAsync());
        if (_mysql is not null)
        {
            MySqlConnectionString = _mysql.GetConnectionString();
        }
    }

    public const string StaffEmail = "staff@plastoids.local";
    public const string StaffPassword = "Staff-Pass-2026!";
    public const string StaffMfaSecret = "JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP";

    private async Task PrepareExternalDatabaseAsync()
    {
        var builder = new MySqlConnectionStringBuilder(_externalMySql!);
        _externalDatabase = "plastoids_test_" + Guid.CreateVersion7().ToString("N")[^8..];
        var admin = new MySqlConnectionStringBuilder(_externalMySql!) { Database = string.Empty };
        await using (var connection = new MySqlConnection(admin.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE `{_externalDatabase}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci";
            await command.ExecuteNonQueryAsync();
        }

        builder.Database = _externalDatabase;
        MySqlConnectionString = builder.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting feeds the host configuration, which Program.cs already sees while registering services
        // (ConfigureAppConfiguration would only apply after Build(), silently leaving appsettings.json values in place).
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Plastoids"] = MySqlConnectionString,
            ["ConnectionStrings:Redis"] = _redis.GetConnectionString(),
            ["Kafka:BootstrapServers"] = _kafka.GetBootstrapAddress(),
            ["Kafka:DefaultPartitions"] = "1",
            ["Kafka:ConsumerBaseBackoffMs"] = "50",
            ["Database:MigrateOnStartup"] = "true",
            ["Diagnostics:Enabled"] = "true",
            ["BlobStorage:ServiceUrl"] = $"http://{_s3.Hostname}:{_s3.GetMappedPublicPort(9090)}",
            ["BlobStorage:Bucket"] = "plastoids-test",
            ["BlobStorage:AccessKey"] = "test",
            ["BlobStorage:SecretKey"] = "test",
            ["Secrets:JwtSigningKey"] = "integration-test-signing-key-0123456789abcdef",
            ["Secrets:FieldEncryptionKey"] = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=",
            ["Secrets:BlindIndexKey"] = "Hx4dHBsaGRgXFhUUExIREA8ODQwLCgkIBwYFBAMCAQA=",
            ["Seed:Enabled"] = "true",
            ["Seed:StaffPassword"] = StaffPassword,
            ["Seed:StaffMfaSecret"] = StaffMfaSecret,
            ["Payments:SweepHoldDays"] = "0",
            ["RateLimiting:AuthPermitsPerMinute"] = "10000",
            ["RateLimiting:GlobalTokensPerMinute"] = "100000",
            ["Admin:IpAllowList:0"] = "127.0.0.1",
            ["Admin:IpAllowList:1"] = "::1",
            ["Serilog:MinimumLevel:Default"] = "Warning",
        };
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        var tasks = new List<Task> { _kafka.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask(), _s3.DisposeAsync().AsTask() };
        if (_mysql is not null)
        {
            tasks.Add(_mysql.DisposeAsync().AsTask());
        }
        else if (_externalDatabase is not null)
        {
            tasks.Add(DropExternalDatabaseAsync());
        }

        await Task.WhenAll(tasks);
    }

    private async Task DropExternalDatabaseAsync()
    {
        var admin = new MySqlConnectionStringBuilder(_externalMySql!) { Database = string.Empty };
        await using var connection = new MySqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS `{_externalDatabase}`";
        await command.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<PlastoidsApiFactory>
{
    public const string Name = "api";
}
