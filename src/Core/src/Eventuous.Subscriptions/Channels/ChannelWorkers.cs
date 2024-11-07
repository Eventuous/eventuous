// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Threading.Channels;

namespace Eventuous.Subscriptions.Channels;

class ChannelWorker<T>(Channel<T> channel, ProcessElement<T> process, bool throwOnFull = false)
    : ChannelWorkerBase<T>(channel, token => channel.Read(process, token), 1, throwOnFull);

/// <summary>
/// Creates a new instance of the channel worker, starts a task for background reads
/// </summary>
/// <param name="channel">Channel to use for writes and reads</param>
/// <param name="process">Function to process each element the worker reads from the channel</param>
/// <param name="concurrencyLevel"></param>
sealed class ConcurrentChannelWorker<T>(Channel<T> channel, ProcessElement<T> process, int concurrencyLevel)
    : ChannelWorkerBase<T>(channel, token => channel.Read(process, token), concurrencyLevel);

class BatchedChannelWorker<T>(Channel<T> channel, ProcessElement<T[]> processor, int maxCount, TimeSpan maxTime, bool throwOnFull = false)
    : ChannelWorkerBase<T>(channel, token => channel.ReadBatches(processor, maxCount, maxTime, token), 1, throwOnFull);
