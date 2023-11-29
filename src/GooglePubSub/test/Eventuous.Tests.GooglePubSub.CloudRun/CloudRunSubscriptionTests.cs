// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Net.Http.Json;
using System.Text.Json;
using Eventuous.GooglePubSub;
using Eventuous.GooglePubSub.CloudRun;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Eventuous.Tests.GooglePubSub.CloudRun;

public class CloudRunSubscriptionTests : IDisposable {
    readonly TestServer _host;

    public CloudRunSubscriptionTests(ITestOutputHelper output) {
        var builder = new WebHostBuilder()
            .Configure(
                app => {
                    app.UseRouting();
                    app.UseEndpoints(ep => CloudRunPubSubSubscription.MapSubscription(ep, app.ApplicationServices));
                }
            )
            .ConfigureServices(
                services => {
                    services.AddLogging(cfg => cfg.AddXunit(output));
                    services.AddSingleton(output);

                    services.AddSubscription<CloudRunPubSubSubscription, CloudRunPubSubSubscriptionOptions>(
                        "test",
                        b => b.AddEventHandler<TestHandler>()
                    );
                    services.AddRouting();
                }
            );
        _host = new TestServer(builder);
    }

    [Fact]
    public async Task CanRegisterEndpoint() {
        var client = _host.CreateClient();

        var pubSubAttributes = new PubSubAttributes();
        TypeMap.RegisterKnownEventTypes();

        var attr = new Dictionary<string, string> {
            [pubSubAttributes.ContentType] = "application/json",
            [pubSubAttributes.EventType]   = "test-event",
            [pubSubAttributes.MessageId]   = Guid.NewGuid().ToString()
        };
        var testEvent = new TestEvent("id", "name");
        var data      = JsonSerializer.SerializeToUtf8Bytes(testEvent);
        var encoded   = Convert.ToBase64String(data);
        var message   = new Message(Guid.NewGuid().ToString(), attr, encoded, DateTime.UtcNow);
        var envelope  = new Envelope(message);
        var response  = await client.PostAsJsonAsync("/", envelope);
        response.EnsureSuccessStatusCode();

        var handler = _host.Services.GetRequiredService<TestHandler>();
        handler.Events.Should().BeEquivalentTo([testEvent]);
    }

    class TestHandler(ITestOutputHelper output) : IEventHandler {
        public string DiagnosticName => "test-handler";

        public List<object?> Events { get; } = new();

        public ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
            output.WriteLine("Received an event");
            Events.Add(context.Message);

            return ValueTask.FromResult(EventHandlingStatus.Success);
        }
    }

    public void Dispose() => _host.Dispose();

    record Message(string MessageId, Dictionary<string, string> Attributes, string Data, DateTime PublishTime);

    record Envelope(Message? Message);

    [EventType("test-event")]
    record TestEvent(string Id, string Name);
}
