// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Diagnostics;

public static class TelemetryTags {
    public static class Net {
        public const string Transport = "net.transport";
        public const string PeerIp    = "net.peer.ip";
        public const string PeerPort  = "net.peer.port";
        public const string PeerName  = "net.peer.name";
        public const string HostIp    = "net.host.ip";
        public const string HostPort  = "net.host.port";
        public const string HostName  = "net.host.name";
    }

    public static class EndUser {
        public const string Id    = "enduser.id";
        public const string Role  = "enduser.role";
        public const string Scope = "enduser.scope";
    }

    public static class Peer {
        public const string Service = "peer.service";
    }

    public static class Db {
        public const string System             = "db.system";
        public const string ConnectionString   = "db.connection_string";
        public const string User               = "db.user";
        public const string MsSqlInstanceName  = "db.mssql.instance_name";
        public const string Name               = "db.name";
        public const string Statement          = "db.statement";
        public const string Operation          = "db.operation";
        public const string Instance           = "db.instance";
        public const string Url                = "db.url";
        public const string CassandraKeyspace  = "db.cassandra.keyspace";
        public const string RedisDatabaseIndex = "db.redis.database_index";
        public const string MongoDbCollection  = "db.mongodb.collection";
    }

    public static class Message {
        public const string Type = "message.type";
        public const string Id   = "message.id";
    }

    public static class Serverless {
        public const string Trigger            = "faas.trigger";
        public const string Execution          = "faas.execution";
        public const string DocumentCollection = "faas.document.collection";
        public const string DocumentOperation  = "faas.document.operation";
        public const string DocumentTime       = "faas.document.time";
        public const string DocumentName       = "faas.document.name";
        public const string Time               = "faas.time";
        public const string Cron               = "faas.cron";
    }

    public static class Messaging {
        public const string System          = "messaging.system";
        public const string Destination     = "messaging.destination";
        public const string DestinationKind = "messaging.destination_kind";
        public const string Url             = "messaging.url";
        public const string MessageId       = "messaging.message_id";
        public const string ConversationId  = "messaging.conversation_id";
        public const string CorrelationId   = "messaging.correlation_id";
        public const string CausationId     = "messaging.causation_id";
        public const string Operation       = "messaging.operation";
    }

    public static class Exception {
        public const string EventName  = "exception";
        public const string Type       = "exception.type";
        public const string Message    = "exception.message";
        public const string Stacktrace = "exception.stacktrace";
    }

    public static class Otel {
        public const string StatusCode        = "otel.status_code";
        public const string StatusDescription = "otel.status_description";
    }

    public static class Eventuous {
        public const string Consumer     = "eventuous.consumer";
        public const string EventHandler = "eventuous.event-handler";
        public const string Subscription = "eventuous.subscription";
        public const string Stream       = "eventuous.stream";
        public const string Partition    = "eventuous.partition";
        public const string Command      = "eventuous.command";
        public const string AppService   = "eventuous.appservice";
    }
}