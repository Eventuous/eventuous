```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.6.1 (24G90) [Darwin 24.6.0]
Apple M2 Ultra, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD
  Job-ERDHQH : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD

IterationCount=5  WarmupCount=3  

```
| Method                                        | Mean      | Error     | StdDev    | Ratio | Gen0   | Allocated | Alloc Ratio |
|---------------------------------------------- |----------:|----------:|----------:|------:|-------:|----------:|------------:|
| &#39;Current: ContextItems with Dictionary&#39;       | 36.471 ns | 0.7360 ns | 0.1911 ns |  1.00 | 0.0315 |     264 B |        1.00 |
| &#39;Optimized: Lazy ContextItems&#39;                | 36.820 ns | 0.6813 ns | 0.1054 ns |  1.01 | 0.0315 |     264 B |        1.00 |
| &#39;Current: HandlingResults with ConcurrentBag&#39; |  4.385 ns | 0.1282 ns | 0.0198 ns |  0.12 | 0.0076 |      64 B |        0.24 |
| &#39;Optimized: HandlingResults single result&#39;    |  4.214 ns | 0.1455 ns | 0.0225 ns |  0.12 | 0.0076 |      64 B |        0.24 |
| &#39;Current: HandlingResults 3 results&#39;          | 59.432 ns | 1.0114 ns | 0.1565 ns |  1.63 | 0.0401 |     336 B |        1.27 |
| &#39;Optimized: HandlingResults 3 results&#39;        | 58.762 ns | 0.9283 ns | 0.2411 ns |  1.61 | 0.0401 |     336 B |        1.27 |
