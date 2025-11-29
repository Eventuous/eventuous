### Project description

A small example demonstrating the use of snapshots stored as an event in the aggregate stream

### How to use snapshots?

First, declare the type of event that represents a snapshot of the current state. In this example, see AccountEvents.V1.Snapshot. This name is chosen just for the example; ideally, it should correspond to something from your domain, for example, the closing of a shift

Once we have a declared snapshot type, we need to link it to the state. Note the declaration of the AccountState type - it has the Snapshots attribute applied to it, which is given a list of types that are snapshot events. This attribute helps the EventReader understand that it needs to read the stream in reverse order up to the first encountered snapshot, and not load events prior to the snapshot

You also need to define the logic according to which the snapshot event will occur. Since a snapshot is a regular event, you can create it at any moment during command processing, based on the available data to make the decision. An example can be seen in AccountService - the ApplySnapshot method.

Essentially, this is all you need to start using snapshots (it is assumed that a package with source generators has been added to the project).