# Eventuous.Subscriptions.Generators

Source generator that creates static conversions from `IMessageConsumeContext` to `IMessageConsumeContext<T>` for
all types `T` that are used in consumers of `IMessageContext<T>` both explicitly or implicitly.