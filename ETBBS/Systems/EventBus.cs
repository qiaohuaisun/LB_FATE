namespace ETBBS;

/// <summary>
/// Simple topic-based pub/sub event bus for decoupling game systems.
/// Allows systems to emit events without direct coupling to handlers.
/// Thread-safe for single-threaded game loops; use locks for multi-threaded scenarios.
/// </summary>
public sealed class EventBus
{
    private readonly Dictionary<string, List<Action<object?>>> _handlers = new(StringComparer.Ordinal);

    /// <summary>
    /// Subscribes a handler to a topic. Returns a disposable token for unsubscription.
    /// </summary>
    /// <param name="topic">Event topic name (e.g., "unit.death", "turn.end").</param>
    /// <param name="handler">Callback to invoke when the topic is published.</param>
    /// <returns>Disposable token - dispose to unsubscribe.</returns>
    public IDisposable Subscribe(string topic, Action<object?> handler)
    {
        if (!_handlers.TryGetValue(topic, out var list))
        {
            list = new List<Action<object?>>();
            _handlers[topic] = list;
        }
        list.Add(handler);
        return new Unsubscriber(list, handler);
    }

    /// <summary>
    /// Publishes an event to all subscribers of a topic.
    /// Handlers are invoked synchronously in registration order.
    /// </summary>
    /// <param name="topic">Event topic name.</param>
    /// <param name="payload">Optional event data payload.</param>
    public void Publish(string topic, object? payload = null)
    {
        if (_handlers.TryGetValue(topic, out var list))
        {
            // ToArray() prevents modification during iteration
            foreach (var h in list.ToArray())
                h(payload);
        }
    }

    private sealed class Unsubscriber : IDisposable
    {
        private readonly List<Action<object?>> _list;
        private readonly Action<object?> _handler;
        public Unsubscriber(List<Action<object?>> list, Action<object?> handler)
        { _list = list; _handler = handler; }
        public void Dispose() => _list.Remove(_handler);
    }
}

