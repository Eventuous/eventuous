namespace ElasticPlayground;

static class ResultExtensions {
    public static void Dump<T>(this Result<T> r) where T : State<T>, new() {
        Console.WriteLine(r.Success ? "Success" : "Failure");

        r.Match(
            ok => {
                foreach (var change in ok.Changes!) {
                    Console.WriteLine($"{change.EventType} {change.Event}");
                }
            },
            error => Console.WriteLine(error.ErrorMessage)
        );
    }
}
