// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using TUnit.Core.Enums;

// SQL Server for Linux has no arm64 build; its amd64 image segfaults on startup under a Mac container runtime.
[assembly: ExcludeOn(OS.MacOs)]
