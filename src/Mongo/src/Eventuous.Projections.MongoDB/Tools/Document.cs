namespace Eventuous.Projections.MongoDB.Tools;

public abstract record Document(string Id);

public abstract record ProjectedDocument(string Id) : Document(Id) {
    public ulong StreamPosition { get; init; }
    public ulong Position { get; init; }
}