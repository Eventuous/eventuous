namespace Eventuous.Projections.MongoDB.Tools;

public abstract record Document(string Id);

public abstract record ProjectedDocument(string Id) : Document(Id) {
    public ulong Position { get; init; }
}