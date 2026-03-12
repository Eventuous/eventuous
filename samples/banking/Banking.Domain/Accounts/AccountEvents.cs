using Eventuous;

namespace Banking.Domain.Accounts;

public static class AccountEvents {
    public static class V1 {
        [EventType("V1.Deposited")]
        public record Deposited(decimal Amount);

        [EventType("V1.Withdrawn")]
        public record Withdrawn(decimal Amount);

        [EventType("V1.Snapshot")]
        public record Snapshot(decimal Balance);
    }
}
