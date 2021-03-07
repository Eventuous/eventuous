namespace CoreLib {
    public record ExpectedStreamVersion(long Value) {
        public static ExpectedStreamVersion NoStream = new(-1);
    }

    public record StreamReadPosition(long Value) {
        public static StreamReadPosition Start = new(0L);
    }
}