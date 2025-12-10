```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.6.1 (24G90) [Darwin 24.6.0]
Apple M2 Ultra, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD
  Job-ERDHQH : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD

IterationCount=5  WarmupCount=3  

```
| Method                              | MessageCount | Mean | Error |
|------------------------------------ |------------- |-----:|------:|
| **&#39;Single message handler invocation&#39;** | **1**            |   **NA** |    **NA** |
| &#39;Batch message processing&#39;          | 1            |   NA |    NA |
| &#39;Context creation + processing&#39;     | 1            |   NA |    NA |
| **&#39;Single message handler invocation&#39;** | **10**           |   **NA** |    **NA** |
| &#39;Batch message processing&#39;          | 10           |   NA |    NA |
| &#39;Context creation + processing&#39;     | 10           |   NA |    NA |
| **&#39;Single message handler invocation&#39;** | **100**          |   **NA** |    **NA** |
| &#39;Batch message processing&#39;          | 100          |   NA |    NA |
| &#39;Context creation + processing&#39;     | 100          |   NA |    NA |

Benchmarks with issues:
  SubscriptionMessageProcessingBenchmarks.'Single message handler invocation': Job-ERDHQH(IterationCount=5, WarmupCount=3) [MessageCount=1]
  SubscriptionMessageProcessingBenchmarks.'Batch message processing': Job-ERDHQH(IterationCount=5, WarmupCount=3) [MessageCount=1]
  SubscriptionMessageProcessingBenchmarks.'Context creation + processing': Job-ERDHQH(IterationCount=5, WarmupCount=3) [MessageCount=1]
  SubscriptionMessageProcessingBenchmarks.'Single message handler invocation': Job-ERDHQH(IterationCount=5, WarmupCount=3) [MessageCount=10]
  SubscriptionMessageProcessingBenchmarks.'Batch message processing': Job-ERDHQH(IterationCount=5, WarmupCount=3) [MessageCount=10]
  SubscriptionMessageProcessingBenchmarks.'Context creation + processing': Job-ERDHQH(IterationCount=5, WarmupCount=3) [MessageCount=10]
  SubscriptionMessageProcessingBenchmarks.'Single message handler invocation': Job-ERDHQH(IterationCount=5, WarmupCount=3) [MessageCount=100]
  SubscriptionMessageProcessingBenchmarks.'Batch message processing': Job-ERDHQH(IterationCount=5, WarmupCount=3) [MessageCount=100]
  SubscriptionMessageProcessingBenchmarks.'Context creation + processing': Job-ERDHQH(IterationCount=5, WarmupCount=3) [MessageCount=100]
