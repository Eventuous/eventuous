create schema if not exists __schema__;

create table if not exists __schema__.snapshots (
    stream_name varchar(1000) not null,
    revision    bigint not null,
    event_type  varchar(128) not null,
    json_data   jsonb not null,
    created     timestamp not null default (now() at time zone 'utc'),
    constraint pk_snapshots primary key (stream_name),
    constraint ck_revision_gte_zero check (revision >= 0)
);

create index if not exists snapshots_stream_name_idx on __schema__.snapshots (stream_name);

