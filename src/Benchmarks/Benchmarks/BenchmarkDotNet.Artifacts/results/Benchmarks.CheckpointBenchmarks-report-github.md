```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.6.1 (24G90) [Darwin 24.6.0]
Apple M2 Ultra, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD
  Job-YEIFEC : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD

InvocationCount=1  IterationCount=5  UnrollFactor=1  
WarmupCount=3  

```
| Method                                    | PositionCount | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------ |-------------- |----------:|-----------:|----------:|----------:|
| **&#39;Sequential checkpoint commits&#39;**           | **10**            |  **4.042 μs** |  **5.2251 μs** | **0.8086 μs** |         **-** |
| &#39;Checkpoint commits with gaps&#39;            | 10            |  3.741 μs |  4.7691 μs | 1.2385 μs |         - |
| &#39;CommitPositionSequence - add sequential&#39; | 10            |  3.441 μs |  1.6490 μs | 0.4283 μs |         - |
| &#39;CommitPositionSequence - add with gaps&#39;  | 10            |  3.157 μs |  3.2165 μs | 0.4978 μs |         - |
| &#39;CommitPositionSequence - gap detection&#39;  | 10            |  6.442 μs |  3.3719 μs | 0.8757 μs |         - |
| &#39;Checkpoint store operations&#39;             | 10            |  1.104 μs |  1.0428 μs | 0.1614 μs |         - |
| **&#39;Sequential checkpoint commits&#39;**           | **100**           | **15.635 μs** | **10.0163 μs** | **1.5500 μs** |   **35584 B** |
| &#39;Checkpoint commits with gaps&#39;            | 100           | 13.291 μs |  4.4320 μs | 1.1510 μs |   20072 B |
| &#39;CommitPositionSequence - add sequential&#39; | 100           | 29.325 μs |  7.3814 μs | 1.9169 μs |    9096 B |
| &#39;CommitPositionSequence - add with gaps&#39;  | 100           | 27.450 μs |  3.8883 μs | 1.0098 μs |    8296 B |
| &#39;CommitPositionSequence - gap detection&#39;  | 100           | 31.016 μs |  6.2328 μs | 1.6186 μs |    9744 B |
| &#39;Checkpoint store operations&#39;             | 100           |  1.167 μs |  0.9212 μs | 0.2392 μs |         - |
