# Obtaining a good link to iMuseum newspaper images

Example of the output: http://corpus.gaelg.im/IMuseumNewspaper/Component/V1?newspaper=IMT&date=1913-05-03&id=Ar00905

## iMuseum steps

* Open the newspaper archive: https://www.imuseum.im/Olive/APA/IsleofMan/?action=search&text=testingdefault#panel=browse
* Select Browse
* Select the newspaper (left sidebar)
* Select the year
* Select the month (left sidebar)
* Scroll to the page in the previewer
* Click once to zoom in
* Double click the section of the article which contains Manx
* Select the 'link' option

## Extracting the component

Once the 'link' hs been selected, the following screen should appear:

https://www.imuseum.im/Olive/APA/IsleofMan/SharedView.Article.aspx?href=IMT%2F1913%2F05%2F03&id=Ar00905&sk=E10B8601&viewMode=image

The component we want is `Ar00905`, listed inside the `&id=Ar00905` section of the URL:

```
https://www.imuseum.im/Olive/APA/IsleofMan/SharedView.Article.aspx?href=IMT%2F1913%2F05%2F03&id=Ar00905&sk=E10B8601&viewMode=image
                                                                                                ^^^^^^^
```

Once this has been obtained, add it into `manifest.json.txt` under `mnhNewsComponent`. Example: https://github.com/david-allison/manx-search-data/commit/bf9b02d39e8dedd0c8b259ffb1c4e1e028bc0ab1

This currently supports multiple articles from the same source: `"mnhNewsComponent": ["Ar00905", "Ar00906"],`. But not from different articles.

## Rationale

Sourcing our data is important. Most newspaper articles already have a text-based source. Example: `Isle of Man Times, Saturday, May 03, 1913; Page: 9`

In order to reduce the effort to proofread, we would like to link to the iMuseum website rather than expecting the user to search. This can save a few minutes per document.

iMuseum provides a 'standard' link to a newspaper, but this doesn't support the scroll wheel. Many users will not realise that the scroll bars are usable, which causes frustration.  Sample: https://www.imuseum.im/Olive/APA/IsleofMan/SharedView.Article.aspx?href=IMT%2F1913%2F05%2F03&id=Ar00905&sk=E10B8601&viewMode=image

We want to do better than this. I (David) have reverse-engineered the newspaper site to provide a usable link: https://corpus.gaelg.im/docs/Manx-Gaelic-Gathering-first-chaglym

The link to the newspaper images appear in the corpus article under 'Additional Data' -> 'Sources'

## Technical Details

* Source Code: 
  * **IMuseumNewspaperService** [Permalink](https://github.com/david-allison/manx-corpus-search/blob/f471c5e52237f0fed232808e002897bf28165a11/CorpusSearch/Service/IMuseumNewspaperService.cs) [Current](https://github.com/david-allison/manx-corpus-search/blob/master/CorpusSearch/Service/IMuseumNewspaperService.cs) 
  * **IMuseumNewspaperController** [Permalink](https://github.com/david-allison/manx-corpus-search/blob/7a11925893790a7be93988ac12cda76da4f8bc17/CorpusSearch/Controllers/IMuseumNewspaperController.cs) [Current](https://github.com/david-allison/manx-corpus-search/blob/master/CorpusSearch/Controllers/IMuseumNewspaperController.cs)
  
Example page: https://corpus.gaelg.im/docs/Manx-Gaelic-Gathering-first-chaglym

* Example URL: 
  * http://corpus.gaelg.im/IMuseumNewspaper/Component/V1?newspaper=IMT&date=1913-05-03&id=Ar00905 
