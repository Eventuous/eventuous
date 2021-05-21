namespace Eventuous {
    public record AppendEventsResult(ulong GlobalPosition, long NextExpectedVersion) {
        public static AppendEventsResult NoOp = new(0, -1);
    }
}