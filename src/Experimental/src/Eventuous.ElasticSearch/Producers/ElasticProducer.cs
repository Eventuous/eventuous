// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Producers;
using Eventuous.Producers.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Eventuous.ElasticSearch.Producers;

public class ElasticProducer(IElasticClient elasticClient, ILogger<ElasticProducer> log) : BaseProducer<ElasticProduceOptions>(TracingOptions) {
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

        var bulk   = GetOp(new(stream.ToString()));
        var result = await elasticClient.BulkAsync(bulk, cancellationToken);

        if (!result.IsValid) {
            if (result.DebugInformation.Contains("version conflict")) {
                log.LogWarning("ElasticProducer: version conflict");
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

        return;

        BulkDescriptor GetOp(BulkDescriptor descriptor)
            => mode switch {
                ProduceMode.Create => GetCreateDescriptor(descriptor),
                ProduceMode.Index  => descriptor.IndexMany(documents),
                _                  => throw new ArgumentOutOfRangeException(nameof(mode))
            };

        BulkDescriptor GetCreateDescriptor(BulkDescriptor descriptor) => descriptor.CreateMany<object>(
            messagesList.Select(x => new ElasticDocument(x.MessageId, x.Message)),
            (createDescriptor, o) => {
                var pm = o as ElasticDocument;

                return createDescriptor.Document(pm!.Message).Id(pm.MessageId);
            }
        );
    }
}

record ElasticDocument(Guid MessageId, object Message);

public record ElasticProduceOptions {
    public ProduceMode ProduceMode { get; init; } = ProduceMode.Create;
}

public enum ProduceMode {
    Create,
    Index
}
