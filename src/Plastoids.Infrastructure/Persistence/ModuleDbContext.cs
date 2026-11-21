using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Plastoids.Infrastructure.Messaging.Outbox;
using Plastoids.Infrastructure.Persistence.Interceptors;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Infrastructure.Persistence;

/// <summary>
/// Base DbContext for every module. Provides:
///  - table prefix "&lt;module&gt;_" and snake_case naming,
///  - UUID v7 as BINARY(16), Money as (amount_kobo, currency) complex columns, DATETIME(6) UTC,
///  - outbox and inbox tables for this module,
///  - tenant query filters on every <see cref="Entity"/>.
/// </summary>
public abstract class ModuleDbContext : DbContext
{
    private readonly ICurrentTenant _tenant;

    protected ModuleDbContext(DbContextOptions options, ICurrentTenant tenant) : base(options)
    {
        _tenant = tenant;
    }

    /// <summary>Lower-case module name used as table prefix, e.g. "identity" → identity_users.</summary>
    public abstract string ModulePrefix { get; }

    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<InboxMessage> Inbox => Set<InboxMessage>();

    /// <summary>Current tenant, evaluated per query so filters follow the ambient context.</summary>
    public Guid CurrentTenantId => _tenant.TenantId;

    public bool BypassTenantFilter => _tenant.IsSystemContext;

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureModule(modelBuilder);
        OutboxConfiguration.Configure(modelBuilder, ModulePrefix);
        ModelConventions.Apply(modelBuilder, this);
    }

    protected abstract void ConfigureModule(ModelBuilder modelBuilder);

    /// <summary>Table name helper so configurations stay consistent: Table("users") → "identity_users".</summary>
    protected string Table(string name) => $"{ModulePrefix}_{name}";
}

public static class ModelConventions
{
    public static void Apply(ModelBuilder modelBuilder, ModuleDbContext context)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned() || entityType.ClrType is null)
            {
                continue;
            }

            // Table prefix for any table that was not explicitly prefixed.
            var table = entityType.GetTableName();
            if (table is not null && !table.StartsWith(context.ModulePrefix + "_", StringComparison.Ordinal))
            {
                entityType.SetTableName($"{context.ModulePrefix}_{table}");
            }

            foreach (var property in entityType.GetProperties())
            {
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (type == typeof(Guid))
                {
                    // Pomelo + MySqlConnector GuidFormat=Binary16 (set in the connection string) serialise big-endian,
                    // which preserves UUID v7 time ordering in the index.
                    property.SetColumnType("binary(16)");
                }
                else if (type == typeof(DateTime))
                {
                    property.SetColumnType("datetime(6)");
                }
                else if (type == typeof(string) && property.GetMaxLength() is null && property.GetColumnType() is null)
                {
                    property.SetMaxLength(255);
                }
            }

            if (typeof(Entity).IsAssignableFrom(entityType.ClrType))
            {
                var builder = modelBuilder.Entity(entityType.ClrType);
                builder.HasKey(nameof(Entity.Id));
                builder.Property(nameof(Entity.RowVersion)).IsConcurrencyToken().HasColumnType("bigint unsigned");
                builder.HasIndex(nameof(Entity.TenantId));
                builder.Ignore(nameof(Entity.DomainEvents));

                // Tenant filter. System contexts (back office, jobs) bypass it and are audited.
                var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                var tenantProperty = System.Linq.Expressions.Expression.Property(parameter, nameof(Entity.TenantId));
                var ctxConst = System.Linq.Expressions.Expression.Constant(context);
                var bypass = System.Linq.Expressions.Expression.Property(ctxConst, nameof(ModuleDbContext.BypassTenantFilter));
                var current = System.Linq.Expressions.Expression.Property(ctxConst, nameof(ModuleDbContext.CurrentTenantId));
                var body = System.Linq.Expressions.Expression.OrElse(bypass, System.Linq.Expressions.Expression.Equal(tenantProperty, current));
                builder.HasQueryFilter(System.Linq.Expressions.Expression.Lambda(body, parameter));
            }

            // Money → complex columns <prop>_kobo BIGINT + <prop>_currency CHAR(3)
            foreach (var clrProperty in entityType.ClrType.GetProperties())
            {
                var propType = Nullable.GetUnderlyingType(clrProperty.PropertyType) ?? clrProperty.PropertyType;
                if (clrProperty.SetMethod is null || entityType.IsIgnored(clrProperty.Name))
                {
                    continue; // computed (get-only) or explicitly ignored Money properties are not columns
                }

                if (propType == typeof(Money) && entityType.FindProperty(clrProperty.Name) is null && entityType.FindComplexProperty(clrProperty.Name) is null)
                {
                    modelBuilder.Entity(entityType.ClrType).ComplexProperty(typeof(Money), clrProperty.Name, cp =>
                    {
                        cp.Property(nameof(Money.AmountKobo)).HasColumnName($"{ToSnake(clrProperty.Name)}_kobo").HasColumnType("bigint");
                        cp.Property(nameof(Money.Currency)).HasColumnName($"{ToSnake(clrProperty.Name)}_currency").HasColumnType("char(3)");
                    });
                }
            }
        }
    }

    public static string ToSnake(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var sb = new System.Text.StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}

public static class ModuleDbContextOptions
{
    /// <summary>Standard options for a module context: shared scoped connection, snake_case, interceptors.</summary>
    public static DbContextOptionsBuilder UsePlastoidsMySql(
        this DbContextOptionsBuilder builder,
        IServiceProvider serviceProvider)
    {
        var connection = serviceProvider.GetRequiredService<ScopedDbConnection>().Connection;
        var options = serviceProvider.GetRequiredService<IOptions<MySqlOptions>>().Value;
        var serverVersion = ServerVersion.Parse(options.ServerVersion);

        // No EnableRetryOnFailure: the retrying execution strategy rejects user-initiated transactions, and every
        // command runs inside one opened by IUnitOfWork (ADR 0002). Transient-failure retries belong at the
        // pipeline/consumer level where the whole unit of work can be replayed.
        return builder
            .UseMySql(connection, serverVersion)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                serviceProvider.GetRequiredService<AuditableEntityInterceptor>(),
                serviceProvider.GetRequiredService<TenantWriteGuardInterceptor>());
    }
}
