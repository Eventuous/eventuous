namespace Eventuous {
    public abstract record AggregateId(string Value) {
        string Value { get; } = Value;
        
        public override string ToString() => Value;

        public static implicit operator string(AggregateId id) => id.Value;
    }
}
