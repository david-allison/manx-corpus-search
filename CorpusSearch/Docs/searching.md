# Searching

As the system is still in beta, these are subject to change. 

The search system is very flexible. Please raise an issue on GitHub if your use-case is not supported and we should be able to assist.

* By default, the system does not differentiate between diacritical marks. `ç` matches `c`. A list can be found: https://github.com/david-allison-manx-corpus-search/blob/master/CorpusSearch/Service/DiacriticService.cs
* By default, the search is case insensitive. `Ayns` matches `ayns` and vice-versa
* Punctuation marks (except ?, - and ') are removed from the search index
* It is currently not possible to search for the words: `and`, `or`, or `not`.
* A query is limited to 30 characters. Please ask if you need this increased.
* There is no limit to the number of results returned. A search for `*` will return all results in the corpus

## Simple Queries

* `ayns` will return all paragraphs containing `ayns`
* `aght cha` will return all results where `aght` is followed by `cha`

Punctuation is ignored, `"aght, cha"` would be returned. 

Use `and` (see `Logical expressions`) if you do not want the words to be directly adjacent

## Logical expressions

`and`, `or` and `not` can be used to further expand or refine the search:

* `ayns and as`: the paragraph must contain both `ayns` and `as`
* `ayns or as`: the paragraph must contain one or both of the words
* `ayns not as`: the paragraph must contain `ayns` and must not contain `as`

## Wildcards

The system allows three wildcards. These can appear at either end, or the middle of a word:

* `*` - matches zero or more characters. `as*` will match `as` and `ashoonyn`
* `+` - matches one or more character. `as+` will match `ashoonyn`, but not `as`
* `_` - matches one character. `agh_` will match `aght` but not `agh` or `aghin`

## Ignore hyphens

By default, hyphens are significant: `lhiam-lhiat`, `lhiam lhiat` and `lhiamlhiat` are three different searches.

Ticking `Ignore hyphens` under `Advanced options` makes hyphens, spaces and joined words interchangeable, so any of the three searches above matches all of:

* `lhiam-lhiat`
* `lhiam lhiat`
* `lhiamlhiat`

The exception: a search with no hyphen or space (`lhiamlhiat`) will not match the spaced form (`lhiam lhiat`), as the system cannot know where the word would be split.
