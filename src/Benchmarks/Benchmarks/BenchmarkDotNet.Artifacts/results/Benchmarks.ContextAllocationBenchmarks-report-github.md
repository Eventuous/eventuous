```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.6.1 (24G90) [Darwin 24.6.0]
Apple M2 Ultra, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD
  Job-ERDHQH : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD

IterationCount=5  WarmupCount=3  

```
| Method                               | Mean       | Error      | StdDev    | Ratio | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |-----------:|-----------:|----------:|------:|-------:|----------:|------------:|
| &#39;Context creation (baseline)&#39;        | 295.475 ns |  2.6459 ns | 0.4095 ns | 1.000 | 0.0467 |     392 B |        1.00 |
| &#39;Context + ContextItems usage&#39;       | 327.477 ns |  6.1686 ns | 0.9546 ns | 1.108 | 0.0753 |     632 B |        1.61 |
| &#39;Typed context wrapper creation&#39;     |   4.134 ns |  0.0297 ns | 0.0046 ns | 0.014 | 0.0029 |      24 B |        0.06 |
| &#39;HandlingResults - single result&#39;    |   4.259 ns |  0.0434 ns | 0.0113 ns | 0.014 | 0.0076 |      64 B |        0.16 |
| &#39;HandlingResults - multiple results&#39; |  60.287 ns |  0.5800 ns | 0.1506 ns | 0.204 | 0.0401 |     336 B |        0.86 |
| &#39;HandlingResults with failure check&#39; |  58.836 ns |  0.6904 ns | 0.1793 ns | 0.199 | 0.0468 |     392 B |        1.00 |
| &#39;ContextItems - no usage (empty)&#39;    |   2.604 ns |  0.0503 ns | 0.0131 ns | 0.009 | 0.0029 |      24 B |        0.06 |
| &#39;ContextItems - add and retrieve&#39;    |  50.947 ns |  0.9284 ns | 0.2411 ns | 0.172 | 0.0315 |     264 B |        0.67 |
| &#39;Full message processing simulation&#39; | 342.573 ns | 12.8273 ns | 1.9850 ns | 1.159 | 0.0753 |     632 B |        1.61 |
