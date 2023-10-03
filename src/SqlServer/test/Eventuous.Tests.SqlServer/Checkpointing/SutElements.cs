namespace Eventuous.Tests.SqlServer.Checkpointing;

public record TestAccount(string UserName);

public record InjectTestAccounts(IList<TestAccount> Accounts);

[EventType("V1.TestAccountInserted")]
public record TestAccountInserted(TestAccount Account);

public class TestAccounts : Aggregate<TestAccountsState> {
    public void InjectAccounts(IList<TestAccount> accounts) {
        foreach (var insertion in accounts) {
            Apply(new TestAccountInserted(insertion));
        }
    }
}

public record TestAccountsState : State<TestAccountsState>;

public record TestAccountsId() : Id("$$Singleton$$") {
    public static readonly TestAccountsId Instance = new();
}