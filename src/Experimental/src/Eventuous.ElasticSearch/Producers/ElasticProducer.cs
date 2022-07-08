// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Diagnostics;
using Eventuous.Producers;
using Eventuous.Producers.Diagnostics;
using Nest;

namespace Eventuous.ElasticSearch.Producers;

public class ElasticProducer : BaseProducer<ElasticProduceOptions> {
    readonly IElasticClient _elasticClient;

    public ElasticProducer(IElasticClient elasticClient) : base(TracingOptions) => _elasticClient = elasticClient;

    static readonly ProducerTracingOptions TracingOptions = new() {
        MessagingSystem  = "elasticsearch",
        DestinationKind  = "datastream",
        ProduceOperation = "create"
    };

    protected override async Task ProduceMessages(
        StreamName                   stream,
        IEnumerable<ProducedMessage> messages,
        ElasticProduceOptions?       options,
        CancellationToken            cancellationToken = default
    ) {
        var messagesList = messages.ToList();
        var documents    = messagesList.Select(x => x.Message);
        var mode         = options?.ProduceMode ?? ProduceMode.Create;

        var bulk   = GetOp(new BulkDescriptor(stream.ToString()));
        var result = await _elasticClient.BulkAsync(bulk, cancellationToken);

        if (!result.IsValid) {
            if (result.DebugInformation.Contains("version conflict")) {
                EventuousEventSource.Log.Warn("ElasticProducer: version conflict");
            }
            else {
                var errors = messagesList
                    .Where(x => result.ItemsWithErrors.Any(y => y.Id == x.MessageId.ToString()))
                    .ToList();

                if (errors.Count == 0) errors = messagesList;

                foreach (var error in errors) {
                    await error.Nack<ElasticProducer>(result.DebugInformation, result.OriginalException).NoContext();
                }

                messagesList = messagesList.Except(errors).ToList();
            }
        }

        await Task.WhenAll(messagesList.Select(x => x.Ack<ElasticProducer>().AsTask())).NoContext();

        BulkDescriptor GetOp(BulkDescriptor descriptor)
            => mode switch {
                ProduceMode.Create => descriptor.CreateMany<object>(
                    messagesList,
                    (createDescriptor, o) => {
                        var pm = o as ProducedMessage;
                        return createDescriptor.Document(pm!.Message).Id(pm.MessageId);
                    }
                ),
                ProduceMode.Index => descriptor.IndexMany(documents),
                _                 => throw new ArgumentOutOfRangeException()
            };
    }
}

public record ElasticProduceOptions {
    public ProduceMode ProduceMode { get; init; } = ProduceMode.Create;
}

public enum ProduceMode {
    Create,
    Index
}
