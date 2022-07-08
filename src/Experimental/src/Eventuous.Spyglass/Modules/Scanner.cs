using System.Reflection;
using Eventuous.Sut.Domain;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NodaTime;

namespace Eventuous.Spyglass.Modules; 

public class Scanner {
    public Scanner() {
        var execAssemblyPath = Assembly.GetExecutingAssembly().Location;
        var directory        = Path.GetDirectoryName(execAssemblyPath);

        var assemblies = Directory.EnumerateFiles(directory!, "*.dll")
            .Select(Assembly.LoadFrom)
            .ToList();

        var testAssembly = assemblies.First(x => x.FullName.Contains("Eventuous.Tests"));

        var aggregateType = typeof(Aggregate);

        var cl = testAssembly
            .ExportedTypes
            .Where(x => DeepBaseType(x, aggregateType))
            .ToList();

        var at        = cl.First();
        var stateType = at.BaseType.GenericTypeArguments[0];
        var idType    = at.BaseType.GenericTypeArguments[1];

        var fakeEvents = FakeEvents();

        // var evt       = JsonSerializer.Deserialize(fakeEvent, TypeMap.GetType("RoomBooked"));

        var     ctor        = at.GetConstructor(Array.Empty<Type>());
        var     aggr        = (Aggregate) ctor.Invoke(null);
        dynamic aggrDynamic = aggr;

        var setting =
            new JsonSerializerSettings {
                ContractResolver = new MyContractResolver(),
                TypeNameHandling = TypeNameHandling.None
            };

        ApplyAndPrint(fakeEvents[0]);
        ApplyAndPrint(fakeEvents[0], fakeEvents[1]);

        void ApplyAndPrint(params object[] events) {
            aggr.ClearChanges();
            aggr.Load(events);
            object state = aggrDynamic.State;
            var    json  = JsonConvert.SerializeObject(state, Formatting.Indented, setting);
            Console.Write(json);
        }

        static bool DeepBaseType(Type t, Type compareWith) {
            while (true) {
                if (t.BaseType == null) return false;
                if (t.BaseType == compareWith) return true;

                t = t.BaseType;
            }
        }
    }

    static object[] FakeEvents()
        => new object[] {
            new BookingEvents.RoomBooked(
                "234",
                LocalDate.MinIsoValue,
                LocalDate.MaxIsoValue,
                100
            ),
            new BookingEvents.BookingPaymentRegistered("444", 100)
        };
}

// class SpyContractResolver : DefaultContractResolver {
//     protected override List<MemberInfo> GetSerializableMembers(Type objectType)
//     {
//         var result = base.GetSerializableMembers(objectType);
//         if (objectType == typeof(MyClass))
//         {
//             var memberInfo = objectType.GetMember("_myField",
//                 BindingFlags.NonPublic | BindingFlags.Instance).Single();
//             result.Add(memberInfo);
//         }
//         return result;
//     }
// }
public class MyContractResolver : DefaultContractResolver {
    protected override IList<JsonProperty> CreateProperties(
        Type                type,
        MemberSerialization memberSerialization
    ) {
        var props = type.GetProperties(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
        );

        var jsonProps = props
            .Where(x => x.Name != "EqualityContract")
            .Select(p => base.CreateProperty(p, memberSerialization))
            .Union(
                type.GetFields(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                    )
                    .Select(f => base.CreateProperty(f, memberSerialization))
            )
            .Where(x => !x.PropertyName.Contains("__BackingField"))
            .ToList();

        jsonProps.ForEach(
            p => {
                p.Writable = true;
                p.Readable = true;
            }
        );

        return jsonProps;
    }
}