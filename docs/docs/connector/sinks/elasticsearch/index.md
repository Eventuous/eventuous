---
title: "Elasticsearch"
description: "Replicate and project events from EventStoreDB to Elasticsearch"
---

An event-sourced system that stores domain events in EventStoreDB could benefit a lot from replicating these events to a search engine, such as Elasticsearch.
When all the events are available in Elasticsearch, the system can be queried for events, and when events properly convey information about business operations, you can discover quite a lot using tools like Kibana.

This connector allows you to replicate events to Elasticsearch without having any knowledge about your system insights like event contracts, or event types. The natural limitation of this approach is that events must be stored in EventStoreDB in JSON format.

## Configuration

The target configuration is used to connect to Elasticsearch, as well as create the necessary elements in Elasticsearch (index template, index rollover policy, and data stream).

The first step is to specify the target assembly in the `connector` section of the configuration file.

```yaml
connector:
  connectorId: "esdb-elastic-connector"
  connectorAssembly: "Eventuous.Connector.EsdbElastic"
```

The target configuration has the following parameters:
* `connectionString` - Elasticsearch connection string, should not be used when the `cloudId` is specified
* `connectorMode` - `producer` or `projector`
* `cloudId` - Elasticsearch cloud id, should be used when the `connectionString` is not specified
* `apiKey` - Elasticsearch API key
* `dataStream` - the index configuration section
    * `indexName` - the index name (data stream name)
    * `template` - the template section
        * `templateName` - the template name
        * `numberOfShards` - the number of shards for the data stream, default is `1`
        * `numberOfReplicas` - the number of replicas for the data stream, default is `1`
    * `lifecycle` - the lifecycle section
        * `policyName` - the rollover policy name
        * `tiers` - the rollover policy tiers, see the structure of a `tier` section below

The `tier` section is used to configure the rollover policy tiers. The tier name must match the available tier in your Elasticsearch cluster.

* `tier` - the tier name (`hot`, `warm`, `cold`, etc)
* `minAge` - the minimum age of the data stream (for example `10d` for 10 days)
* `priority` - the priority of the tier (`0` is the lowest priority)
* `rollover` - the rollover policy section
    * `maxAge` - the maximum index age
    * `maxSize` - the maximum index size
    * `maxDocs` - the maximum index documents
* `forceMerge` - the force merge policy section
    * `maxNumSegments` - the maximum number of segments
* `readOnly` - if the tier will be read only
* `delete` - if the tier will be deleted

## Producer mode

When running in Producer mode, the Connector will replicate events from EventStoreDB to Elasticsearch using data streams. Documents in the data stream are immutable, which is a good choice for storing events.

Based on the configuration, the connector will create the following elements in Elasticsearch:
* Index template
* Data stream
* Index rollover policy

You can optimize the rollover policy to keep the index size optimal, as well as move older events to a cheaper storage tier.

Events are replicated to Elasticsearch in the following format:

* `messageId` - the unique identifier of the event
* `messageType` - the type of the event
* `streamPosition` - event position in the original stream
* `stream` - original stream name
* `globalPosition` - position of the event in the global stream (`$all`)
* `message` - the event payload
* `metadata` - flattened event metadata
* `@timestamp` - the timestamp of the event

There's no need for a sidecar to run the Connector in Producer mode.

## Projector mode

WIP
