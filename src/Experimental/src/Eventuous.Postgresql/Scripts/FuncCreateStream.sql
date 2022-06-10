create or replace function __schema__.create_stream(
    _stream_name varchar(1000)
) returns integer
as $$
declare _stream_id integer;
begin
    insert into __schema__.streams (stream_name, version) values (_stream_name, -1);
    select stream_id into _stream_id from __schema__.streams where stream_name = _stream_name;
    return _stream_id;
end;

$$ language 'plpgsql';
