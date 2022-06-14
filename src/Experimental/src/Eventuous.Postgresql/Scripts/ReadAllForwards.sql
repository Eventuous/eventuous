create or replace function __schema__.read_all_forwards(
    _from_position bigint,
    _count integer
)
returns table (
    message_id uuid,
    message_type varchar,
    stream_position integer,
    global_position bigint,
    json_data jsonb,
    json_metadata jsonb,
    created timestamp,
    stream_name varchar
)
as $$
begin
    return query select m.message_id, m.message_type, 
                        m.stream_position, m.global_position,
                        m.json_data, m.json_metadata, m.created,
                        s.stream_name
        from __schema__.messages m 
        inner join __schema__.streams s on s.stream_id = m.stream_id
        where m.global_position >= _from_position
        limit _count;
end;

$$ language 'plpgsql';