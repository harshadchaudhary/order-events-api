using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderEvents.Infrastructure.Caching;
using OrderEvents.Infrastructure.Data;
using OrderEvents.Infrastructure.Messaging;
using StackExchange.Redis;

namespace OrderEvents.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the data, cache and messaging stack shared by the API and the
    /// worker, so both read the same configuration and behave identically.
    /// </summary>
    public static IServiceCollection AddOrderEventsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));

        var database = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
                       ?? new DatabaseOptions();

        services.AddDbContext<OrderDbContext>(options =>
        {
            if (database.Provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(database.ConnectionString, sql =>
                    // SQL Server in a container is frequently still starting up
                    // when the app connects; retry instead of crash-looping.
                    sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), null));
            }
            else
            {
                options.UseInMemoryDatabase("order-events");
            }
        });

        services.AddScoped<IOrderRepository, OrderRepository>();

        var redis = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();

        if (redis.Enabled)
        {
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var config = ConfigurationOptions.Parse(redis.ConnectionString);
                config.AbortOnConnectFail = false; // reconnect in the background instead of failing startup
                return ConnectionMultiplexer.Connect(config);
            });
            services.AddSingleton<IOrderCache, RedisOrderCache>();
        }
        else
        {
            services.AddSingleton<IOrderCache, NullOrderCache>();
        }

        var kafka = configuration.GetSection(KafkaOptions.SectionName).Get<KafkaOptions>() ?? new KafkaOptions();

        if (kafka.Enabled)
        {
            services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
        }
        else
        {
            services.AddSingleton<IEventPublisher, LoggingEventPublisher>();
        }

        return services;
    }

    /// <summary>
    /// Creates the schema on startup. Fine for a demo and for the in-memory
    /// provider; a production service would apply EF Core migrations instead.
    /// </summary>
    public static async Task EnsureDatabaseCreatedAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

        try
        {
            await db.Database.EnsureCreatedAsync(ct);
            logger.LogInformation("Database ready");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not prepare the database");
            throw;
        }
    }
}
