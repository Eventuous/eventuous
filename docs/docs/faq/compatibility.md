---
title: "Compatibility"
description: "Platforms and SDKs"
---

## Only .NET 6+? Why not .NET Framework 3.5?

Eventuous uses the latest features of C#, like records and advanced pattern matching. Therefore, we rely on compiler versions which supports C# 11.

We also aim to support the current application hosting model that only got consistent and stable in .NET 6+.

Eventuous supports .NET Core 3.1, but it's not a priority. Some packages only support .NET 6 and .NET 7 as they need the latest features like minimal API. Right now, Eventuous provides packages for the following runtimes:

- .NET Core 3.1
- .NET 6
- .NET 7

Targets will be added and removed when getting our of support or when new versions get released.

