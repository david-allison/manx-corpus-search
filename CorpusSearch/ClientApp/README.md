# Manx Corpus Search - Single Page Application

The React + TypeScript SPA for **Manx Corpus Search**. 

Served by the parent [`CorpusSearch`](https://github.com/david-allison/manx-corpus-search) ASP.NET Core application, 
communicating via `fetch`.

## Stack

- **React 18** + **TypeScript** (from Create React App)
- **react-router-dom 6**
- **MUI** (`@mui/material`) and **reactstrap** / **Bootstrap 5** for UI

## Getting started

Run the parent `CorpusSearch` app in Development mode.

### Routes

| Path             | Purpose                                                               |
|------------------|-----------------------------------------------------------------------|
| `/`              | Search the corpus and lists documents                                 |
| `/docs/:docId`   | Side-by-side bilingual view of a single document & in-document search |
| `/tools/youtube` | Tool to assist in subtitling on YouTube                               |

### Other routes

Some routes are handled outside the SPA by the ASP.NET app. See `CorpusSearch/Views`

Examples:

* Browse documents
  * HTML documents, to be crawled without JS. 
* Cregeen's Dictionary
* Mailing List subscription
