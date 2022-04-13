namespace ElasticPlayground;

static class ResultExtensions {
    public static void Dump<T, TId>(this Result<T, TId> r)
        where T : AggregateState<T, TId>, new()
        where TId : AggregateId {
        Console.WriteLine(r.Success ? "Success" : "Failure");

        switch (r) {
            case OkResult<T, TId> ok:
                foreach (var change in ok.Changes!) {
                    Console.WriteLine($"{change.EventType} {change.Event}");
                }

                break;
            case ErrorResult<T, TId> error:
                Console.WriteLine(error.ErrorMessage);
                break;
        }
    }
}
