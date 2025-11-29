using Eventuous;

namespace Banking.Domain.Accounts;

[Snapshots(typeof(AccountEvents.V1.Snapshot))]
public record AccountState : State<AccountState> {
    public decimal Balance { get; init; }

    public AccountState() {
        On<AccountEvents.V1.Snapshot>(When);
        On<AccountEvents.V1.Deposited>(When);
        On<AccountEvents.V1.Withdrawn>(When);
    }

    private AccountState When(AccountState state, AccountEvents.V1.Snapshot e) => state with {
        Balance = e.Balance
    };

    private AccountState When(AccountState state, AccountEvents.V1.Deposited e) => state with {
        Balance = state.Balance + e.Amount
    };

    private AccountState When(AccountState state, AccountEvents.V1.Withdrawn e) => state with {
        Balance = state.Balance - e.Amount
    };
}
