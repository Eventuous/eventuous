```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.6.1 (24G90) [Darwin 24.6.0]
Apple M2 Ultra, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD
  Job-ERDHQH : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD

IterationCount=5  WarmupCount=3  

```
| Method                                           | Mean          | Error       | StdDev     | Median        | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------- |--------------:|------------:|-----------:|--------------:|------:|--------:|-------:|-------:|----------:|------------:|
| &#39;OLD: HandlingResults ConcurrentBag (single)&#39;    |   725.3432 ns | 358.2608 ns | 93.0392 ns |   679.7854 ns | 1.012 |    0.16 | 0.1411 | 0.0706 |    1184 B |        1.00 |
| &#39;NEW: HandlingResults Optimized (single)&#39;        |     4.3629 ns |   0.0833 ns |  0.0129 ns |     4.3599 ns | 0.006 |    0.00 | 0.0076 |      - |      64 B |        0.05 |
| &#39;OLD: HandlingResults ConcurrentBag (3 results)&#39; | 1,031.5773 ns | 177.1218 ns | 27.4098 ns | 1,021.8274 ns | 1.439 |    0.16 | 0.1926 | 0.0954 |    1624 B |        1.37 |
| &#39;NEW: HandlingResults Optimized (3 results)&#39;     |    62.4788 ns |   3.1560 ns |  0.8196 ns |    62.4005 ns | 0.087 |    0.01 | 0.0401 |      - |     336 B |        0.28 |
| &#39;OLD: ContextItems eager Dictionary (not used)&#39;  |    10.9220 ns |   0.4695 ns |  0.1219 ns |    10.9095 ns | 0.015 |    0.00 | 0.0124 |      - |     104 B |        0.09 |
| &#39;NEW: ContextItems lazy Dictionary (not used)&#39;   |     2.8759 ns |   0.0791 ns |  0.0205 ns |     2.8781 ns | 0.004 |    0.00 | 0.0029 |      - |      24 B |        0.02 |
| &#39;OLD: ContextItems eager Dictionary (used)&#39;      |    37.2149 ns |   1.6123 ns |  0.4187 ns |    37.1999 ns | 0.052 |    0.01 | 0.0315 |      - |     264 B |        0.22 |
| &#39;NEW: ContextItems lazy Dictionary (used)&#39;       |    36.6316 ns |   2.1428 ns |  0.3316 ns |    36.7412 ns | 0.051 |    0.01 | 0.0315 |      - |     264 B |        0.22 |
| &#39;OLD: Logging scope with Dictionary&#39;             |    36.7883 ns |   1.4737 ns |  0.2281 ns |    36.8573 ns | 0.051 |    0.01 | 0.0258 |      - |     216 B |        0.18 |
| &#39;NEW: Logging scope with KeyValuePair array&#39;     |     8.9584 ns |   0.1901 ns |  0.0494 ns |     8.9539 ns | 0.012 |    0.00 | 0.0086 |      - |      72 B |        0.06 |
| &#39;OLD: Always create linked CTS&#39;                  |     4.9044 ns |   0.1118 ns |  0.0290 ns |     4.9187 ns | 0.007 |    0.00 | 0.0057 |      - |      48 B |        0.04 |
| &#39;NEW: Guarded CTS creation (same tokens)&#39;        |     0.0052 ns |   0.0041 ns |  0.0006 ns |     0.0054 ns | 0.000 |    0.00 |      - |      - |         - |        0.00 |
| &#39;NEW: Guarded CTS creation (non-cancelable)&#39;     |     0.0049 ns |   0.0172 ns |  0.0045 ns |     0.0031 ns | 0.000 |    0.00 |      - |      - |         - |        0.00 |
