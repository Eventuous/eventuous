create or replace function __schema__.read_stream_backwards(
    _stream_name varchar(1000),
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
declare
    _stream_id integer;
begin
    select s.stream_id into _stream_id
    from __schema__.streams s
    where s.stream_name = _stream_name;
    
    if _stream_id is null then
        raise exception 'StreamNotFound';
    end if;
    
    return query select m.message_id, m.message_type, m.stream_position, m.global_position,
                        m.json_data, m.json_metadata, m.created
        from __schema__.messages m 
        where m.stream_id = _stream_id
        order by m.stream_position desc
        limit _count;
end;

$$ language 'plpgsql';