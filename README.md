<a href="https://manxcorpus.com/"><img src="https://img.shields.io/uptimerobot/status/m788600664-01eee56ee2b6a032b98b70c4"></a>

# manx-corpus-search

A corpus search for primarily bilingual manx to english texts.

Deployed at https://manxcorpus.com/

To add/modify documents, see: [manx-search-data](https://github.com/david-allison-1/manx-search-data)

## Installation

1. Clone the source
2. Copy the `OpenData` folder from [manx-search-data](https://github.com/david-allison-1/manx-search-data/) into `CorpusSearch/OpenData` folder
3. `dotnet run`

## Tech Stack

* React
* C# (ASP.NET Core, both WebAPI and content server)
* Document Searching: [Apache Lucene.NET](https://github.com/apache/lucenenet)
* Query Search Syntax: [csly](https://github.com/b3b00/csly)
* CSV: [CsvHelper](https://github.com/JoshClose/CsvHelper)
* JSON: Newtonsoft.Json

## Aims

* Run in RAM on a cheap (<$20/m) droplet
* No expectation of scaling up for a large number of users
* Expected corpus size is unlikely to exceed 10MM words of Manx (and 10MM words of English)
* Stateless

## Deployment

Deployable on a $5 DigitalOcean droplet. See GitHub actions
