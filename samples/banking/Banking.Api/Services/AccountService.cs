// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Banking.Domain.Accounts;
using Eventuous;

namespace Banking.Api.Services;

public class AccountService : CommandService<AccountState> {
    public record Deposit(string AccountId, decimal Amount);
    public record Withdraw(string AccountId, decimal Amount);

    public AccountService(IEventStore store) : base(store) {
        On<Deposit>()
            .InState(ExpectedState.Any)
            .GetStream(cmd => StreamName.ForState<AccountState>(cmd.AccountId))
            .Act(ApplySnapshot<Deposit>(Handle));

        On<Withdraw>()
            .InState(ExpectedState.Any)
            .GetStream(cmd => StreamName.ForState<AccountState>(cmd.AccountId))
            .Act(ApplySnapshot<Withdraw>(Handle));
    }

    private IEnumerable<object> Handle(AccountState state, IEnumerable<object> _, Deposit cmd) {
        return [
            new AccountEvents.V1.Deposited(cmd.Amount)
        ];
    }

    private IEnumerable<object> Handle(AccountState state, IEnumerable<object> _, Withdraw cmd) {
        if (state.Balance < cmd.Amount) {
            throw new InvalidOperationException();
        }

        return [
            new AccountEvents.V1.Withdrawn(cmd.Amount)
        ];
    }

    Func<AccountState, IEnumerable<object>, TCommand, IEnumerable<object>> ApplySnapshot<TCommand>(Func<AccountState, IEnumerable<object>, TCommand, IEnumerable<object>> handler) => (state, events, command) => {
        var newEvents = handler(state, events, command);

        if (newEvents.Count() + events.Count() >= 10) {
            foreach (var @event in newEvents) {
                state = state.When(@event);
            }
            return [
                ..newEvents,
                    new AccountEvents.V1.Snapshot(state.Balance)
            ];
        }

        return newEvents;
    };
}
