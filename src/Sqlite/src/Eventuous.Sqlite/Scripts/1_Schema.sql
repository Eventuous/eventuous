CREATE TABLE IF NOT EXISTS __schema___streams (
    stream_id   INTEGER PRIMARY KEY AUTOINCREMENT,
    stream_name TEXT    NOT NULL UNIQUE,
    version     INTEGER NOT NULL DEFAULT(-1)
);

CREATE TABLE IF NOT EXISTS __schema___messages (
    global_position INTEGER PRIMARY KEY AUTOINCREMENT,
    message_id      TEXT    NOT NULL,
    message_type    TEXT    NOT NULL,
    stream_id       INTEGER NOT NULL REFERENCES __schema___streams(stream_id),
    stream_position INTEGER NOT NULL,
    json_data       TEXT    NOT NULL,
    json_metadata   TEXT,
    created         TEXT    NOT NULL,
    UNIQUE(stream_id, stream_position),
    UNIQUE(stream_id, message_id),
    CHECK(stream_position >= 0)
);

CREATE INDEX IF NOT EXISTS idx___schema___messages_stream_id ON __schema___messages(stream_id);

CREATE TABLE IF NOT EXISTS __schema___checkpoints (
    id       TEXT PRIMARY KEY,
    position INTEGER NULL
);
