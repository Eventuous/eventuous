create or replace function __schema__.truncate_stream(
    _stream_name varchar(1000),
    _expected_version integer,
    _position integer
)
returns table (success bit)
as $$
declare
    _current_version integer;
    _stream_id integer;
begin
    select s.version, s.stream_id into _current_version, _stream_id
    from __schema__.streams s
    where s.stream_name = _stream_name;
    
    if _stream_id is null then
        raise exception 'StreamNotFound';
    end if;

    if _expected_version != -2 and _expected_version != _current_version then
        raise exception 'WrongExpectedVersion %, current version %', _expected_version, _current_version;
    end if;
    
    if _current_version < _position then
        return;
    end if;
    
    delete from __schema__.messages m where m.stream_id = _stream_id and m.stream_position < _position;
end;

$$ language 'plpgsql';