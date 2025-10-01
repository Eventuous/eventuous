using System.Reflection;
using System.Reflection.Emit;

namespace Benchmarks.Tools;

public class DynamicAssembly {
    readonly ModuleBuilder _dynamicModule;

    public DynamicAssembly() {
        var assemblyName    = new AssemblyName(Guid.NewGuid().ToString());
        var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        _dynamicModule = dynamicAssembly.DefineDynamicModule("Main");
    }

    public Type GenerateType() {
        var newTypeName     = Guid.NewGuid().ToString();

        var dynamicType = _dynamicModule.DefineType(
            newTypeName,
            TypeAttributes.Public          |
            TypeAttributes.Class           |
            TypeAttributes.AutoClass       |
            TypeAttributes.AnsiClass       |
            TypeAttributes.BeforeFieldInit |
            TypeAttributes.AutoLayout,
            null
        );

        dynamicType.DefineDefaultConstructor(
            MethodAttributes.Public      |
            MethodAttributes.SpecialName |
            MethodAttributes.RTSpecialName
        );

        return dynamicType.CreateType();
    }
}