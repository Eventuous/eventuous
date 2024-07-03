using BenchmarkDotNet.Attributes;
using Benchmarks.Tools;
using Eventuous;

namespace Benchmarks;

[MemoryDiagnoser]
public class TypeMapBenchmark {
    KeyValuePair<string, Type>[] _types = null!;

    [Params(5, 20, 100)]
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public int TypesCount { get; set; }

    [GlobalSetup]
    public void Setup() {
        var assembly = new DynamicAssembly();
        _types = new KeyValuePair<string, Type>[TypesCount];

        for (var i = 0; i < TypesCount; i++) {
            var type = assembly.GenerateType();
            _types[i] = new KeyValuePair<string, Type>(i.ToString(), type);
            _v1.AddType(_types[i].Value, _types[i].Key);
        }

        var random = new Random(DateTimeOffset.Now.Millisecond);

        for (var i = 0; i < GetCount; i++) {
            var pos = random.Next(0, TypesCount - 1);
            _keysToFind[i]  = _types[pos].Key;
            _typesToFind[i] = _types[pos].Value;
        }
    }

    readonly TypeMapper _v1 = new();

    const int GetCount = 1000;

    readonly string[] _keysToFind  = new string[GetCount];
    readonly Type[]   _typesToFind = new Type[GetCount];

    [Benchmark]
    public void GetTypes() {
        for (var i = 0; i < GetCount; i++) {
            var type = _v1.GetType(_keysToFind[i]);
        }
    }
}
