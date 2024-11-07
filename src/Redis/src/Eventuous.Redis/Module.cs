// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;

namespace Eventuous.Redis;

public class Module {
    static readonly Assembly Assembly = typeof(Module).Assembly;

    public static async Task LoadModule(GetRedisDatabase getDatabase) {
        var names = Assembly.GetManifestResourceNames()
            .Where(x => x.EndsWith(".lua"))
            .OrderBy(x => x);

        var db = getDatabase();

        foreach (var name in names) {
            await using var stream = Assembly.GetManifestResourceStream(name);
            using var       reader = new StreamReader(stream!);

            var script = await reader.ReadToEndAsync().NoContext();

            try {
                await db.ExecuteAsync("FUNCTION", "LOAD", "REPLACE", script).NoContext();
            }
            catch (Exception e) {
                if (!e.Message.Contains("'append_events' already exists")) throw;
            }
        }
    }
}
