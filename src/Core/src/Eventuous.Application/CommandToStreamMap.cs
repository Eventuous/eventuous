// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public delegate StreamName GetStreamNameFromCommand<in TCommand>(TCommand command) where TCommand : class;

delegate ValueTask<StreamName> GetStreamNameFromUntypedCommand(object command, CancellationToken cancellationToken);

public class CommandToStreamMap {
    readonly TypeMap<GetStreamNameFromUntypedCommand> _typeMap = new();

    public void AddCommand<TCommand>(GetStreamNameFromCommand<TCommand> getId) where TCommand : class
        => _typeMap.Add<TCommand>((cmd, _) => new ValueTask<StreamName>(getId((TCommand)cmd)));

    internal bool TryGet<TCommand>([NotNullWhen(true)] out GetStreamNameFromUntypedCommand? getId) where TCommand : class
        => _typeMap.TryGetValue<TCommand>(out getId);
}
