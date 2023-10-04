namespace Eventuous.Tests.SqlServer.Checkpointing;

class TestCommandService : CommandService<TestAccounts, TestAccountsState, TestAccountsId> {
    public TestCommandService(IAggregateStore store)
        : base(store) {
        On<InjectTestAccounts>()
            .InState(ExpectedState.Any)
            .GetId(_ => TestAccountsId.Instance)
            .Act((accounts, cmd) => accounts.InjectAccounts(cmd.Accounts));
    }
}
