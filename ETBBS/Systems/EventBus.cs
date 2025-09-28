namespace ETBBS;

public sealed class EventBus
{
    private readonly Dictionary<string, List<Action<object?>>> _handlers = new(StringComparer.Ordinal);

    public IDisposable Subscribe(string topic, Action<object?> handler)
    {
        if (!_handlers.TryGetValue(topic, out var list))
        {
            list = new List<Action<object?>>() ;
            _handlers[topic] = list;
        }
        list.Add(handler);
        return new Unsubscriber(list, handler);
    }

    public void Publish(string topic, object? payload = null)
    {
        if (_handlers.TryGetValue(topic, out var list))
        {
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
 
