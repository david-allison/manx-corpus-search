# Contributing

## Getting started

1. Clone the source
2. Copy the `OpenData` folder from [manx-search-data](https://github.com/david-allison/manx-search-data/) into `CorpusSearch/OpenData` folder
3. `dotnet run`

For UI-only changes, run the React app against the live site:

```sh
cd CorpusSearch/ClientApp && npm run dev:live
```

## Pre-commit hooks

Hooks are managed with [pre-commit](https://pre-commit.com/). Install the hook once per clone:

```sh
brew install pre-commit  # or: pip install pre-commit
pre-commit install
```

## Tech Stack

* React
* C# (ASP.NET Core, both WebAPI and content server)
* Document Searching: [Apache Lucene.NET](https://github.com/apache/lucenenet)
* Query Search Syntax: [csly](https://github.com/b3b00/csly)
* CSV: [CsvHelper](https://github.com/JoshClose/CsvHelper)
* JSON: Newtonsoft.Json