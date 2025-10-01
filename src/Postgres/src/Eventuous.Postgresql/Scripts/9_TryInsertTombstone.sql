create or replace function __schema__.try_insert_tombstone(
    _gap_position bigint,
    _stream_name text,
    _message_type text,
    _message_id uuid
) returns void
as $$
declare sid int;
        next_pos int;
begin
    -- Ensure stream exists
    insert into __schema__.streams(stream_name, version)
    values (_stream_name, -1)
    on conflict (stream_name) do nothing;

    select stream_id into sid from __schema__.streams where stream_name=_stream_name;

    -- Allocate next stream position via atomic version bump
    update __schema__.streams
        set version = version + 1
        where stream_id = sid
        returning version into next_pos;

    -- Attempt tombstone insert at the gap global position
    insert into __schema__.messages(
        message_id, message_type, stream_id, stream_position,
        global_position, json_data, json_metadata, created
    ) overriding system value
    values (_message_id, _message_type, sid, next_pos, _gap_position, '{}'::jsonb, '{}'::jsonb, (now() at time zone 'utc'))
    on conflict do nothing;
end;

$$ language 'plpgsql';