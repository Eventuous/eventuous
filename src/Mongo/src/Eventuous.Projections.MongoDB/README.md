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
            evt => evt.ProjectId, 
            (evt, update) => update.SetOnInsert(x => x.ProjectName, evt.Name)
        );
        On<V1.TaskCreated>(
            evt => evt.ProjectId,
            (evt, update) => update.AddToSet(
                x => x.Tasks,
                new ProjectTaskRecord(evt.TaskId, evt.Description)
            )
        );
    }
}
```

There's an overload for `On<TEvent>` that gives you direct access to the consume context, so you can have more advanced operations.

```csharp
On<V1.ProjectRegistered>(ctx => { ... });
```

The function provided as an argument for `On` in this case needs to return `ValueTask<Operation<T>>`. 
To make things easier, we provide a function called `UpdateOperationTask`.

The `UpdateOperationTask` function has two overloads. 
The one used in the example allows passing the document id as the first argument.
Another overload allows specifying a custom filter as `(filter, evt) => filter.Eq(x => x.Field == evt.Value)`.

You can also use the `UpdateOperation`, which allows you to perform asynchronous operations inside the update function.

There is also a handler registration function for building updates asynchronously called `OnAsync`, which works similar t `On`:

```csharp
OnAsync<V1.ProjectRegistered>(
    evt => evt.ProjectId, 
    async (evt, update) => update.SetOnInsert(
        x => x.ProjectName, 
        await getExternalData(evt.Name)
    )
);
```

You also have an option of not using pre-registered typed handlers by overriding the `GetUpdate` function. 
By default it returns `NoOp`, so if you override it, don't call `base.GetUpdate`. Eventuous will only call `GetUpdate` if there's no registered handler for a received event.

The `GetUpdate` function must return an instance of the `Operation<T>` record wrapped in `ValueTask`.
`UpdateOperation` and `UpdateOperationTask` return `ValueTask<UpdateOperation<T>>`. You can use one of the supplied record types:
- `UpdateOperation<T>` - includes a filter and an update operation
- `CollectionOperation<T>` - allows executing any operation on the collection
- `OtherOperation<T>` - allows executing anything as it will be just awaited
