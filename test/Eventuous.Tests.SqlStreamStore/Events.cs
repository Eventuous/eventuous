using System;
using Eventuous;

namespace Eventuous.Tests.SqlStreamStore {
    public static class Events {
        public record AccountCreated(Guid AccountNumber);
        public record AmountLodged(decimal Amount);
        public record AmountWithdrawn(decimal Amount);
        
        public static void MapEvents() {
            TypeMap.AddType<AccountCreated>("AccountCreated");
            TypeMap.AddType<AmountLodged>("AmountLodged");
            TypeMap.AddType<AmountWithdrawn>("AmountWithdrawn");
        }
    }
}