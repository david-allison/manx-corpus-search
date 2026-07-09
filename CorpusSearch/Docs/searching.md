# Searching

As the system is still in beta, these are subject to change. 

The search system is very flexible. Please raise an issue on GitHub if your use-case is not supported and we should be able to assist.

* By default, the system does not differentiate between diacritical marks. `ç` matches `c` (see `Match accents`). A list can be found: https://github.com/david-allison-manx-corpus-search/blob/master/CorpusSearch/Service/DiacriticService.cs
* By default, the search is case insensitive. `Ayns` matches `ayns` and vice-versa (see `Match case`)
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

## Match case

By default, the search is case insensitive: `moir` matches `Moir`, `moir` and `MOIR`.

Ticking `Match case` under `Advanced options` makes the search case sensitive: `Moir` no longer matches `moir`.

Diacritics are still normalized independently of this option (see `Match accents`): with `Match case` ticked, `Chengey` matches `Çhengey`, but not `çhengey`.

## Match accents

By default, diacritics are ignored: `chengey` matches `çhengey` and vice-versa.

Ticking `Match accents` under `Advanced options` makes diacritics significant: `chengey` no longer matches `çhengey`.

Case is still ignored independently of this option: with `Match accents` ticked, `çhengey` matches `Çhengey`, but not `chengey`.
