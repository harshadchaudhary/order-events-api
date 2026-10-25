namespace OrderEvents.Contracts;

/// <summary>
/// Topic names shared by the producer (API) and the consumer (worker), so a
/// rename can never leave the two sides publishing and listening to different
/// topics.
/// </summary>
public static class KafkaTopics
{
    public const string OrderPlaced = "orders.placed";
    public const string OrderProcessed = "orders.processed";
}
