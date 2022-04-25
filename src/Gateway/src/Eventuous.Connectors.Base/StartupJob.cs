using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Eventuous.Connectors.Base;

public interface IStartupJob {
    Task Run();
}

public class StartupJob<T1, T2> : IStartupJob {
    readonly T1                 _t1;
    readonly T2                 _t2;
    readonly Func<T1, T2, Task> _func;

    public StartupJob(T1 t1, T2 t2, Func<T1, T2, Task> func) {
        _t1   = t1;
        _t2   = t2;
        _func = func;
    }

    public Task Run() => _func(_t1, _t2);
}

public static class StartupJobRegistration {
    public static void AddStartupJob<T1, T2>(this IServiceCollection services, Func<T1, T2, Task> func)
        where T1 : class where T2 : class
        => services.AddSingleton(func).AddSingleton<IStartupJob, StartupJob<T1, T2>>();
}
