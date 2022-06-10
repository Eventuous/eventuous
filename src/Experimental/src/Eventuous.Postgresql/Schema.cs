// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Postgresql;

public class Schema {
    readonly string _schema;

    public Schema(string schema) => _schema = schema;

    public string StreamMessage => $"{_schema}.stream_message";
    public string AppendEvents  => $"{_schema}.append_events";
}
