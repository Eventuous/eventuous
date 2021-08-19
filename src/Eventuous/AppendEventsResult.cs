namespace Eventuous {
    public record AppendEventsResult(ulong GlobalPosition, long NextExpectedVersion) {
        public static readonly AppendEventsResult NoOp = new(0, -1);
    }
}