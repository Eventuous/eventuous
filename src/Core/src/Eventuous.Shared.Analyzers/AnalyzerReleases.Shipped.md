## Release 0.16.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
EV001   | Usage    | Warning  | Event is being emitted but doesn't have the `EventType` attribute applied. Persisting this event might fail if there's no explicit type mapping made at runtime.