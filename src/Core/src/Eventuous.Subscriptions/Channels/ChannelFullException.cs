// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions.Channels; 

public class ChannelFullException() : Exception("Channel worker unable to write to the channel because it's full");