// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Postgresql;

public class Schema {
    readonly string _schema;

    public Schema(string schema) => _schema = schema;

    public string StreamMessage      => $"{_schema}.stream_message";
    public string AppendEvents       => $"{_schema}.append_events";
    public string ReadStreamForwards => $"{_schema}.read_stream_forwards";
    public string ReadAllForwards    => $"{_schema}.read_all_forwards";
    public string StreamExists       => $"select exists (select 1 from {_schema}.streams where stream_name = (@name))";
}
