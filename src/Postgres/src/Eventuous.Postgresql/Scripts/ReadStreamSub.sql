create or replace function __schema__.read_stream_sub(
    _stream_id integer,
    _from_position integer,
    _count integer
)
returns table (
    message_id uuid,
    message_type varchar,
    stream_position integer,
    global_position bigint,
    json_data jsonb,
    json_metadata jsonb,
    created timestamp
)
as $$
begin
    return query select m.message_id, m.message_type, m.stream_position, m.global_position,
                        m.json_data, m.json_metadata, m.created
        from __schema__.messages m 
        where m.stream_id = _stream_id and m.stream_position >= _from_position
        limit _count;
end;

$$ language 'plpgsql';