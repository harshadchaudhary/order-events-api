namespace OrderEvents.Contracts;

/// <summary>
/// Lifecycle of an order. The API only ever creates orders in
/// <see cref="Pending"/>; everything after that is decided by the worker.
/// </summary>
public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Rejected = 2
}
