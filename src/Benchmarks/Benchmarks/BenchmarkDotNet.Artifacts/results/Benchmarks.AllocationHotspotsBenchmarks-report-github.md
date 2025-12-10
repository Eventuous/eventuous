```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.6.1 (24G90) [Darwin 24.6.0]
Apple M2 Ultra, 1 CPU, 24 logical and 24 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD
  Job-ERDHQH : .NET 10.0.0 (10.0.25.52411), Arm64 RyuJIT AdvSIMD

IterationCount=5  WarmupCount=3  

```
| Method                                   | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------------------------------------- |-----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Dictionary for logging scope (current)&#39; |  35.759 ns | 1.2471 ns | 0.3239 ns |  1.00 |    0.01 | 0.0258 |     216 B |        1.00 |
| &#39;Array of KeyValuePairs (alternative)&#39;   |   7.397 ns | 0.0962 ns | 0.0250 ns |  0.21 |    0.00 | 0.0086 |      72 B |        0.33 |
| &#39;Activity name - string interpolation&#39;   |   9.762 ns | 0.2301 ns | 0.0598 ns |  0.27 |    0.00 | 0.0143 |     120 B |        0.56 |
| &#39;Activity name - string concat&#39;          |   9.700 ns | 0.2673 ns | 0.0694 ns |  0.27 |    0.00 | 0.0143 |     120 B |        0.56 |
| &#39;Activity name - StringBuilder&#39;          |  26.851 ns | 0.3184 ns | 0.0493 ns |  0.75 |    0.01 | 0.0382 |     320 B |        1.48 |
| &#39;LINQ Any() check on small list&#39;         |  14.943 ns | 0.3061 ns | 0.0795 ns |  0.42 |    0.00 | 0.0105 |      88 B |        0.41 |
| &#39;Manual iteration on small list&#39;         |  14.169 ns | 0.1175 ns | 0.0182 ns |  0.40 |    0.00 | 0.0105 |      88 B |        0.41 |
| &#39;LINQ Where().Any() pattern&#39;             |  20.077 ns | 0.1967 ns | 0.0511 ns |  0.56 |    0.00 | 0.0134 |     112 B |        0.52 |
| &#39;LINQ Any() with predicate&#39;              |  20.732 ns | 0.4041 ns | 0.0625 ns |  0.58 |    0.01 | 0.0134 |     112 B |        0.52 |
| &#39;Manual enumeration check&#39;               |  19.101 ns | 0.2116 ns | 0.0549 ns |  0.53 |    0.00 | 0.0134 |     112 B |        0.52 |
| &#39;CancellationTokenSource creation&#39;       |   3.672 ns | 0.1086 ns | 0.0282 ns |  0.10 |    0.00 | 0.0057 |      48 B |        0.22 |
| &#39;Linked CancellationTokenSource&#39;         |  61.154 ns | 1.2554 ns | 0.3260 ns |  1.71 |    0.02 | 0.0554 |     464 B |        2.15 |
| Guid.ToString()                          | 263.820 ns | 5.2322 ns | 1.3588 ns |  7.38 |    0.07 | 0.0114 |      96 B |        0.44 |
| &#39;DateTime.UtcNow allocation&#39;             |  16.093 ns | 0.0466 ns | 0.0072 ns |  0.45 |    0.00 |      - |         - |        0.00 |
