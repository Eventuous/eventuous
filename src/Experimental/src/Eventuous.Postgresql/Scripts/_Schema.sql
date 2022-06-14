create schema if not exists __schema__;

create table if not exists __schema__.streams (
    stream_name varchar(1000) not null,
    stream_id   integer primary key generated always as identity,
    version     integer not null default(-1),
    constraint uq_streams_name unique (stream_name),
    constraint ck_version_gte_negative_one check (version >= -1)
);

create table if not exists __schema__.messages (
    message_id      uuid,
    message_type    varchar not null,
    stream_id       integer not null,
    stream_position integer not null,
    global_position bigint primary key generated always as identity, 
    json_data       jsonb not null,
    json_metadata   jsonb,
    created         timestamp not null,
    constraint fk_messages_stream foreign key (stream_id) references __schema__.streams (stream_id),
    constraint uq_messages_stream_id_and_stream_position unique (stream_id, stream_position),
    constraint uq_stream_id_and_message_id unique (stream_id, message_id),
    constraint ck_stream_position_gte_zero check (stream_position >= 0)
);

create index if not exists events_stream_idx on __schema__.messages (stream_id);

create table if not exists __schema__.checkpoints (
    id varchar primary key, 
    position bigint null 
);

do $$
begin
    create type __schema__.stream_message as (
        message_id    uuid,
        message_type  varchar(128),
        json_data     jsonb,
        json_metadata jsonb);
    exception
        when duplicate_object then null;
end $$;
