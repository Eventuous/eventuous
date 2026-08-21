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
    -- The row lock is held until the end of the caller's transaction,
    -- serialising concurrent appends to the same stream
    select s.version, s.stream_id into _current_version, _stream_id
                              from __schema__.streams s
                              where s.stream_name = _stream_name
                              for update;
    if _stream_id is null then -- Stream doesn't exist
        if _expected_version != -2 -- Any
        and _expected_version != -1 then -- NoStream
            raise exception 'StreamNotFound';
        end if;

        -- A concurrent transaction may create the same stream; do nothing then,
        -- and the re-read below locks the winner's row and gets its version
        insert into __schema__.streams (stream_name, version) values (_stream_name, -1)
            on conflict (stream_name) do nothing;
        select s.stream_id, s.version into _stream_id, _current_version
                                  from __schema__.streams s
                                  where stream_name = _stream_name
                                  for update;
    end if;

    if _expected_version != -2 and _expected_version != _current_version then
        raise exception 'WrongExpectedVersion %, current version %', _expected_version, _current_version;
    end if;

    return query select _stream_id, _current_version;
end;

$$ language 'plpgsql';
