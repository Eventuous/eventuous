namespace ElasticPlayground;

static class ResultExtensions {
    public static void Dump<T>(this Result<T> r) where T : State<T>, new() {
        Console.WriteLine(r.Success ? "Success" : "Failure");

        switch (r) {
            case OkResult<T> ok:
                foreach (var change in ok.Changes!) {
                    Console.WriteLine($"{change.EventType} {change.Event}");
                }

                break;
            case ErrorResult<T> error:
                Console.WriteLine(error.ErrorMessage);
                break;
        }
    }
}
