// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Banking.Domain.Accounts;
using Eventuous;

namespace Banking.Api.Services;

public class AccountService : CommandService<AccountState> {
    public record Deposit(string AccountId, decimal Amount);
    public record Withdraw(string AccountId, decimal Amount);

    public AccountService(IEventStore store, ISnapshotStore snapshotStore) : base(store, snapshotStore: snapshotStore) {

        UseSnapshotStrategy(
            predicate: (events, _) => events.Count() >= 5,
            produce: (_, state) => new AccountEvents.V1.Snapshot(state.Balance));

        On<Deposit>()
            .InState(ExpectedState.Any)
            .GetStream(cmd => StreamName.ForState<AccountState>(cmd.AccountId))
            .Act((_, __, cmd) => [new AccountEvents.V1.Deposited(cmd.Amount)]);

        On<Withdraw>()
            .InState(ExpectedState.Any)
            .GetStream(cmd => StreamName.ForState<AccountState>(cmd.AccountId))
            .Act(static (state, __, cmd) => {
                if (state.Balance < cmd.Amount) {
                    throw new InvalidOperationException();
                }

                return [new AccountEvents.V1.Withdrawn(cmd.Amount)];
            });
    }
}
