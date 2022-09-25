// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions.Channels; 

public class ChannelFullException : Exception {
    public ChannelFullException() : base("Channel worker unable to write to the channel because it's full") { }
}