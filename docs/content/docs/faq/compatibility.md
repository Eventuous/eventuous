---
title: "FAQ: Compatibility"
description: "Platforms and SDKs"
date: 2020-10-06T08:49:31+00:00
lastmod: 2020-10-06T08:49:31+00:00
draft: false
images: []
menu:
  docs:
    parent: "faq"
weight: 920
toc: true
---

## Only .NET 5? Why not .NET Framework 3.5?

Eventuous uses the latest features of C#, like records and advanced pattern matching. Therefore, we rely on compiler versions, which support C# 9.

We also aim to support the current application hosting model that only got consistent and stable in .NET 5.

Eventuous will never be available on earlier .NET SDKs. If you need one, consider cloning the repository and making it work.

