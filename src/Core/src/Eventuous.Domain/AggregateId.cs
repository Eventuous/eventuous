namespace Eventuous;

[PublicAPI]
public abstract record AggregateId {
    protected AggregateId(string value) {
        if (string.IsNullOrWhiteSpace(value)) throw new Exceptions.InvalidIdException(this);

        Value = value;
    }

    public string Value { get; }

    public sealed override string ToString() => Value;

    public static implicit operator string(AggregateId? id)
        => id?.ToString() ?? throw new Exceptions.InvalidIdException(typeof(AggregateId));

    public void Deconstruct(out string value) => value = Value;
}
