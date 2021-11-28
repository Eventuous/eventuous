namespace Eventuous.RabbitMq.Producers; 

class ExchangeCache {
    public void EnsureExchange(string name, Action createExchange) {
        if (_exchanges.Contains(name)) return;

        createExchange();
        _exchanges.Add(name);
    }

    readonly HashSet<string> _exchanges = new();
}