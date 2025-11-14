from `src/SqlServer/test/Eventuous.Tests.SqlServer.Database/docker`
run the following:

```sh
docker compose up --build -d
```

You can then use SSMS to connect to the database:

- Server: localhost,11433
- Username: sa
- Password: Password1!

In another window from `src/SqlServer/test/Eventuous.Tests.SqlServer.Database/docker`
run the following

`run-class`
```bash
# Run all tests in a specific class
docker compose run \
  --rm --build \
  eventuous-db-test-runner run-class append_events
```

`run`
Run a single test by full test name
```bash
# Run a single test by full test name
docker compose run \
  --rm --build \
  eventuous-db-test-runner run "[append_events].[Test multiple messages]"
```

`run-all`
**not recommended** as it takes minutes to run all tests. Leave that up to CI build agents unless you really want to run all tests locally.
```bash
# Run all tSQLt tests
docker compose run \
  --rm --build \
  eventuous-db-test-runner run-all
```
