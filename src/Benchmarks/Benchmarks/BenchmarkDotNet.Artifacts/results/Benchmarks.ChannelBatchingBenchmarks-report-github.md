```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.6.1 (24G90) [Darwin 24.6.0]
Apple M2 Ultra, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD
  Job-QMNUBS : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD
  Job-ERDHQH : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD

IterationCount=5  WarmupCount=3  

```
| Method                                           | Job        | InvocationCount | UnrollFactor | BatchSize | Mean       | Error       | StdDev     | Median     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------------- |----------- |---------------- |------------- |---------- |-----------:|------------:|-----------:|-----------:|------:|--------:|-------:|----------:|------------:|
| **&#39;Alternative 2b: ArrayPool with return overhead&#39;** | **Job-QMNUBS** | **1**               | **1**            | **10**        | **24.8000 ns** | **142.7182 ns** | **37.0635 ns** |  **0.0000 ns** |     **?** |       **?** |      **-** |         **-** |           **?** |
|                                                  |            |                 |              |           |            |             |            |            |       |         |        |           |             |
| &#39;Current: List.ToArray()&#39;                        | Job-ERDHQH | Default         | 16           | 10        |  5.5116 ns |   0.1332 ns |  0.0346 ns |  5.5131 ns | 1.000 |    0.01 | 0.0076 |      64 B |        1.00 |
| &#39;Alternative 1: CollectionsMarshal.AsSpan()&#39;     | Job-ERDHQH | Default         | 16           | 10        |  0.0000 ns |   0.0000 ns |  0.0000 ns |  0.0000 ns | 0.000 |    0.00 |      - |         - |        0.00 |
| &#39;Alternative 2: ArrayPool rent/copy&#39;             | Job-ERDHQH | Default         | 16           | 10        | 10.2304 ns |   0.3892 ns |  0.1011 ns | 10.2198 ns | 1.856 |    0.02 | 0.0105 |      88 B |        1.38 |
| &#39;Alternative 3: Pre-allocated array with CopyTo&#39; | Job-ERDHQH | Default         | 16           | 10        |  6.2246 ns |   0.2015 ns |  0.0312 ns |  6.2225 ns | 1.129 |    0.01 | 0.0076 |      64 B |        1.00 |
| &#39;Alternative 4: Direct List (no copy)&#39;           | Job-ERDHQH | Default         | 16           | 10        |  0.0017 ns |   0.0091 ns |  0.0024 ns |  0.0000 ns | 0.000 |    0.00 |      - |         - |        0.00 |
| &#39;Alternative 5: IReadOnlyList wrapper&#39;           | Job-ERDHQH | Default         | 16           | 10        |  4.1141 ns |   0.0671 ns |  0.0174 ns |  4.1077 ns | 0.746 |    0.01 | 0.0029 |      24 B |        0.38 |
|                                                  |            |                 |              |           |            |             |            |            |       |         |        |           |             |
| **&#39;Alternative 2b: ArrayPool with return overhead&#39;** | **Job-QMNUBS** | **1**               | **1**            | **50**        | **83.2000 ns** | **376.4130 ns** | **97.7533 ns** | **41.0000 ns** |     **?** |       **?** |      **-** |         **-** |           **?** |
|                                                  |            |                 |              |           |            |             |            |            |       |         |        |           |             |
| &#39;Current: List.ToArray()&#39;                        | Job-ERDHQH | Default         | 16           | 50        | 12.0382 ns |   0.1448 ns |  0.0376 ns | 12.0435 ns | 1.000 |    0.00 | 0.0268 |     224 B |        1.00 |
| &#39;Alternative 1: CollectionsMarshal.AsSpan()&#39;     | Job-ERDHQH | Default         | 16           | 50        |  0.0000 ns |   0.0000 ns |  0.0000 ns |  0.0000 ns | 0.000 |    0.00 |      - |         - |        0.00 |
| &#39;Alternative 2: ArrayPool rent/copy&#39;             | Job-ERDHQH | Default         | 16           | 50        | 19.3441 ns |   0.3516 ns |  0.0913 ns | 19.3115 ns | 1.607 |    0.01 | 0.0335 |     280 B |        1.25 |
| &#39;Alternative 3: Pre-allocated array with CopyTo&#39; | Job-ERDHQH | Default         | 16           | 50        | 12.8857 ns |   0.2359 ns |  0.0613 ns | 12.8699 ns | 1.070 |    0.01 | 0.0268 |     224 B |        1.00 |
| &#39;Alternative 4: Direct List (no copy)&#39;           | Job-ERDHQH | Default         | 16           | 50        |  0.0000 ns |   0.0000 ns |  0.0000 ns |  0.0000 ns | 0.000 |    0.00 |      - |         - |        0.00 |
| &#39;Alternative 5: IReadOnlyList wrapper&#39;           | Job-ERDHQH | Default         | 16           | 50        |  4.0931 ns |   0.0715 ns |  0.0186 ns |  4.0939 ns | 0.340 |    0.00 | 0.0029 |      24 B |        0.11 |
|                                                  |            |                 |              |           |            |             |            |            |       |         |        |           |             |
| **&#39;Alternative 2b: ArrayPool with return overhead&#39;** | **Job-QMNUBS** | **1**               | **1**            | **100**       |  **0.0000 ns** |   **0.0000 ns** |  **0.0000 ns** |  **0.0000 ns** |     **?** |       **?** |      **-** |         **-** |           **?** |
|                                                  |            |                 |              |           |            |             |            |            |       |         |        |           |             |
| &#39;Current: List.ToArray()&#39;                        | Job-ERDHQH | Default         | 16           | 100       | 21.4336 ns |   0.5448 ns |  0.0843 ns | 21.4648 ns | 1.000 |    0.00 | 0.0507 |     424 B |        1.00 |
| &#39;Alternative 1: CollectionsMarshal.AsSpan()&#39;     | Job-ERDHQH | Default         | 16           | 100       |  0.0000 ns |   0.0000 ns |  0.0000 ns |  0.0000 ns | 0.000 |    0.00 |      - |         - |        0.00 |
| &#39;Alternative 2: ArrayPool rent/copy&#39;             | Job-ERDHQH | Default         | 16           | 100       | 30.6041 ns |   0.7195 ns |  0.1869 ns | 30.6931 ns | 1.428 |    0.01 | 0.0641 |     536 B |        1.26 |
| &#39;Alternative 3: Pre-allocated array with CopyTo&#39; | Job-ERDHQH | Default         | 16           | 100       | 23.3649 ns |   0.2840 ns |  0.0738 ns | 23.3650 ns | 1.090 |    0.00 | 0.0507 |     424 B |        1.00 |
| &#39;Alternative 4: Direct List (no copy)&#39;           | Job-ERDHQH | Default         | 16           | 100       |  0.0000 ns |   0.0000 ns |  0.0000 ns |  0.0000 ns | 0.000 |    0.00 |      - |         - |        0.00 |
| &#39;Alternative 5: IReadOnlyList wrapper&#39;           | Job-ERDHQH | Default         | 16           | 100       |  4.0679 ns |   0.2249 ns |  0.0348 ns |  4.0845 ns | 0.190 |    0.00 | 0.0029 |      24 B |        0.06 |
