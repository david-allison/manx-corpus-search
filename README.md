# manx-corpus-search

A corpus search for primarily bilingual manx to english texts.

Live site: https://corpus.gaelg.im/

To add/modify documents, see: [manx-search-data](https://github.com/david-allison/manx-search-data)

## Aims

* Run in RAM on a cheap (<$20/m) droplet
* No expectation of scaling for a large number of users
* Expected corpus size is unlikely to exceed 10MM words of Manx (and 10MM words of English)
* No backups needed (Stateless and immutable when running - updated and reindexed at night).


## Development
See [CONTRIBUTING.md](CONTRIBUTING.md).

## Local hosting

The corpus can be hosted on your machine using [Docker](https://www.docker.com/). It should run on any modern computer.
Run the following command then navigate to http://127.0.0.1:8080

```sh
docker pull ghcr.io/david-allison/manx-corpus-search:master
docker run --rm -p 127.0.0.1:8080:8080 ghcr.io/david-allison/manx-corpus-search:master
```