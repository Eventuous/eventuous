# Eventuous MongoDB projections

This package adds MongoDB projections to applications built with Eventuous.

There are two main components provided by this package:
- `MongoCheckpointStore`, implements `ICheckpointStore`
- `MongoProjection`, implements `IEventHandler`

## Using checkpoint store

You can register `MongoCheckpointStore` so it will be used by all subscriptions that need to store checkpoints (normally, EventStoreDB catch-up subscriptions).

```csharp
services.AddSingleton<ICheckpointStore, MongoCheckpointStore>();
```

It will add a `checkpoint` collection to your Mongo database. Each subscription will have its own checkpoint document. The document id would be the subscription id.
Make sure to use unique subscription ids across different applications if they use the same Mongo database.

## Using projections

Create your own projection class that inherits `MongoProjection<T>` abstract class.
Here, `T` is the document type, which must be a record. Your document type should inherit from `ProjectedDocument` record.

Use the `On<TEvent>` function to register event projection handlers:

```csharp
public class ProjectWithTasksProjection : MongoProjection<ProjectWithTasks> {
    public ProjectWithTasksProjection(
        IMongoDatabase  database,
        ILoggerFactory? loggerFactory
    ) : base(database, QuerySubscription.Id, loggerFactory) { 
        On<V1.ProjectRegistered>(
            ctx => ctx.Message.ProjectId, 
            (ctx, update) => update.SetOnInsert(x => x.ProjectName, ctx.Message.Name)
        );
        On<V1.TaskCreated>(
            ctx => ctx.Message.ProjectId,
            (ctx, update) => update.AddToSet(
                x => x.Tasks,
                new ProjectTaskRecord(ctx.Message.TaskId, ctx.Message.Description)
            )
        );
    }
}
```

Read more in Eventuous [documentation](http://localhost:1313/docs/read-models/mongo-projections/).