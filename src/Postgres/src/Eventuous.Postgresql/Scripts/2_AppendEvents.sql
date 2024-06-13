create or replace function __schema__.append_events(
    _stream_name varchar(1000),
    _expected_version integer,
    _created timestamp with time zone,
    _messages __schema__.stream_message[]
)
returns table (new_version integer, global_position bigint)
as $$
declare
    _current_version integer;
    _stream_id integer;
    _position bigint;
begin
    if _created is null then
        _created = now() at time zone 'utc';
    end if;
    select s.stream_id, s.new_version 
        into _stream_id, _current_version 
        from __schema__.check_stream(_stream_name, _expected_version) s;
    
    insert into __schema__.messages (message_id, message_type, stream_id, stream_position, 
                                     json_data, json_metadata, created) 
    select m.message_id, m.message_type, _stream_id, 
           _current_version + (row_number() over ()) :: int,
           m.json_data, m.json_metadata, _created
    from unnest(_messages) m
    on conflict do nothing;
    
    select m.stream_position, m.global_position into _current_version, _position
        from __schema__.messages m
        where m.stream_id = _stream_id
        order by m.global_position desc limit 1;
    update __schema__.streams set version = _current_version where stream_id = _stream_id;
    
    return query select _current_version, _position;
end;

$$ language 'plpgsql';
