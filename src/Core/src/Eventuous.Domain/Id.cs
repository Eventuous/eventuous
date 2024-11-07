// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

[Obsolete("Use Id instead")]
public abstract record AggregateId : Id {
    protected AggregateId(string value) : base(value) { }
}

[PublicAPI]
public abstract record Id {
    protected Id(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new Exceptions.InvalidIdException(this);
        }

        Value = value;
    }

    public string Value { get; }

    public sealed override string ToString() => Value;

    public static implicit operator string(Id? id) => id?.ToString() ?? throw new Exceptions.InvalidIdException(typeof(Id));

    public void Deconstruct(out string value) => value = Value;
}
