# Contributing guidelines

Here are simple rules that we expect to be followed by contributors:

- Preferrably, open an issue before submitting a contribution, unless there's an existing issue you are solving.
- Restrain yourself from rants and complaints when you contribute in issues and PR discussions. Respect the people who maintain this project.
- Avoid large PRs addressing multiple issues at once.
- Avoid making unrelated changes like reformatting the code in places that aren't related to the actual change.
- Use the code formats of the repository, we have them in the `.editorconfig` file. Reformat the code accordingly if you copy it from somewhere.
- Only contribute with your own work, or if you have a permission from the others to contribute their code.

## Code style checks

The reusable [code-style workflow](.github/workflows/code-style.yml) runs for pull requests and pushes to `dev`.

Run the same style verification locally from the repository root:

```sh
dotnet format style Eventuous.slnx --verify-no-changes --severity warn
```

This checks Roslyn code-style rules at warning or error severity, including naming conventions and unnecessary imports. Apply available fixes with:

```sh
dotnet format style Eventuous.slnx --severity warn
```

Some violations, such as naming-rule violations, require manual fixes. Rerun verification after making changes. CI also fails if the formatter reports that required project references could not be loaded.

Thanks for contributing!
