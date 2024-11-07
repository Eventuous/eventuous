// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Hosting;

namespace Eventuous.Producers;

public interface IProducer {
    /// <summary>
    /// Produce a message wrapped in the <see cref="ProducedMessage"/>
    /// </summary>
    /// <param name="stream">Stream name where the message should be produced</param>
    /// <param name="messages">Collection of messages to produce</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task Produce(StreamName stream, IEnumerable<ProducedMessage> messages, CancellationToken cancellationToken = default);
}

[PublicAPI]
public interface IProducer<in TProduceOptions> : IProducer where TProduceOptions : class {
    /// <summary>
    /// Produce a message wrapped in the <see cref="ProducedMessage"/>.
    /// </summary>
    /// <param name="stream">Stream name where the message should be produced</param>
    /// <param name="messages">Collection of messages to produce</param>
    /// <param name="options">Produce options</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task Produce(StreamName stream, IEnumerable<ProducedMessage> messages, TProduceOptions? options, CancellationToken cancellationToken = default);
}

public interface IHostedProducer : IHostedService {
    bool Ready { get; }
}
