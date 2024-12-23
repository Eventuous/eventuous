// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Persistence;

namespace Eventuous.Shared;

public interface IDefineAppendAmendment<out TCommand> where TCommand : class {
    /// <summary>
    /// Amends the proposed append before it gets stored.
    /// </summary>
    /// <param name="amendAppend">A function to amend the proposed append</param>
    /// <returns></returns>
    void AmendAppend(AmendAppend<TCommand> amendAppend);
}
