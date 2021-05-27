using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Eventuous;
using FluentAssertions;
using static Eventuous.Tests.SqlStreamStore.Events;

namespace Eventuous.Tests.SqlStreamStore
{
    public class StoringAggregates: InMemoryFixture // MsSqlFixture
    {
        readonly IAggregateStore _aggregateStore; 

        public StoringAggregates() : base(){
            MapEvents();
            _aggregateStore = new AggregateStore(EventStore, Serializer);
        }

        [Fact]
        public async Task StoreOneAggregate()
        {
            // arrange
            var accountId = Guid.NewGuid().ToString();

            // act
            //var account = new BankAccount();
            var account = BankAccount.CreateAccount(new BankAccountId(accountId));
            account.LodgeAmount(100);
            account.WithdrawAmount(20);
            await _aggregateStore.Store(account, new CancellationToken());
            var savedAccount = await _aggregateStore.Load<BankAccount>(accountId, new CancellationToken());
            
            // assert
            savedAccount.CurrentVersion.Should().Be(2);
            savedAccount.State.Id.Value.Should().Be(accountId);
            savedAccount.State.Balance.Should().Be(80);
        }

        [Fact]
        public async Task StoreOneAggregateMultpleTimes()
        {
            // arrange
            var accountId = Guid.NewGuid().ToString();

            // act
            var account = BankAccount.CreateAccount(new BankAccountId(accountId));
            account.LodgeAmount(100);
            account.WithdrawAmount(20);
            await _aggregateStore.Store(account, new CancellationToken());
            var savedAccount = await _aggregateStore.Load<BankAccount>(accountId, new CancellationToken());
            savedAccount.WithdrawAmount(30);
            await _aggregateStore.Store(savedAccount, new CancellationToken());
            var savedAccount2 = await _aggregateStore.Load<BankAccount>(accountId, new CancellationToken());
            
            // assert
            
            savedAccount2.State.Id.Value.Should().Be(accountId);
            savedAccount2.State.Balance.Should().Be(50);
        }

    }
}