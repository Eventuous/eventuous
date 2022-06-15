create or replace function __schema__.check_stream(
    _stream_name varchar(1000),
    _expected_version integer
)
returns table (stream_id integer, new_version integer)
as $$
declare
    _current_version integer;
    _stream_id integer;
begin
    select s.version, s.stream_id into _current_version, _stream_id 
                              from __schema__.streams s
                              where s.stream_name = _stream_name;
    if _stream_id is null then -- Stream doesn't exist
        if _expected_version = -2 -- Any
        or _expected_version = -1 then -- NoStream
            insert into __schema__.streams (stream_name, version) values (_stream_name, -1);
            select s.stream_id, s.version into _stream_id, _current_version 
                                      from __schema__.streams s
                                      where stream_name = _stream_name;
        else
            raise exception 'StreamNotFound';
        end if;
    else -- Stream exists
        if _expected_version != -2 and _expected_version != _current_version then
            raise exception 'WrongExpectedVersion %, current version %', _expected_version, _current_version;
        end if;
    end if;
    
    return query select _stream_id, _current_version;
end;

$$ language 'plpgsql';