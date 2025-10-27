// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Consumers;
using Eventuous.Subscriptions.Context;
using Eventuous.Tests.Projections.MongoDB.Fixtures;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Projections.MongoDB;

public class ContextConversions {
    public static IEnumerable<Func<(IMessageConsumeContext, Type)>> GetArgs() {
        yield return () => (CreateContext<BookingImported>(), typeof(IMessageConsumeContext<BookingImported>));
        yield return () => (CreateContext<RoomBooked>(), typeof(IMessageConsumeContext<RoomBooked>));
        yield return () => (CreateContext<BookingPaymentRegistered>(), typeof(IMessageConsumeContext<BookingPaymentRegistered>));
        yield return () => (CreateContext<BookingCancelled>(), typeof(IMessageConsumeContext<BookingCancelled>));

        yield break;

        static MessageConsumeContext<T> CreateContext<T>() where T : class => new(TestContext<T>.CreateContext());
    }

    [Test]
    [MethodDataSource(nameof(GetArgs))]
    public async Task TestContextConversion(IMessageConsumeContext context, Type expectedType) {
        await Assert.That(MessageConsumeContextConverter.RegisteredConverters).HasCount(1);

        var typed = MessageConsumeContextConverter.RegisteredConverters[0].Invoke(context);
        await Assert.That(typed).IsNotNull();
        await Assert.That(typed.GetType().IsAssignableTo(expectedType)).IsTrue();
    }

    [Test]
    public async Task EnsureReflectionsNotUsed() {
        foreach (var args in GetArgs()) {
            _ = args().Item1.ConvertToGeneric();
        }

        await Assert.That(MessageConsumeContextConverter.ConversionCache).IsEmpty();
    }
}
