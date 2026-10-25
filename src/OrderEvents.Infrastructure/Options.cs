namespace OrderEvents.Infrastructure;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>
    /// When false nothing is produced and no consumer is started — the API can
    /// then be run without a broker. Always true in Docker and in production.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Consumer group id. Only the worker uses this.</summary>
    public string ConsumerGroupId { get; set; } = "order-events-worker";

    /// <summary>
    /// Producer acks. "all" means the leader waits for the in-sync replicas
    /// before acknowledging — slower, but a broker failure can't silently lose
    /// an accepted order.
    /// </summary>
    public string Acks { get; set; } = "all";

    public int MessageTimeoutMs { get; set; } = 10_000;
}

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>How long a cached order stays valid before it is read again.</summary>
    public int CacheSeconds { get; set; } = 60;

    /// <summary>
    /// When false the app runs without Redis and every read goes to the
    /// database. Useful locally and for tests.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>"SqlServer" or "InMemory".</summary>
    public string Provider { get; set; } = "InMemory";

    public string ConnectionString { get; set; } = string.Empty;
}
