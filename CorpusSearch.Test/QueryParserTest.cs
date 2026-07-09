using CorpusSearch.Dependencies;
using CorpusSearch.Model;
using CorpusSearch.Test.TestUtils;
using NUnit.Framework;

namespace CorpusSearch.Test;

[TestFixture]
public class QueryParserTest : QueryBase
{
    [Test]
    public void BasicQuery()
    {
        this.AddManxDoc("1", "hello world");

        var result = Query("hello");

        Assert.That(result.NumberOfMatches, Is.EqualTo(1));
        Assert.That(result.NumberOfSegments, Is.EqualTo(1));
    }

    [Test]
    public void MultipleWords()
    {
        this.AddManxDoc("1", "hello hello hello");

        var result = Query("hello");

        Assert.That(result.NumberOfMatches, Is.EqualTo(3));
        Assert.That(result.NumberOfSegments, Is.EqualTo(1));
    }

    [Test]
    public void SearchIsNotCaseSensitive()
    {
        // the default; see TestCaseSensitiveSearch for the opt-in (#19)
        this.AddManxDoc("1", "Hello");

        var result = Query("hello");

        Assert.That(result.NumberOfMatches, Is.EqualTo(1));
        Assert.That(result.NumberOfSegments, Is.EqualTo(1));
    }

    [Test]
    public void TestSearchForCaps()
    {
        // the default; see TestCaseSensitiveSearch for the opt-in (#19)
        this.AddManxDoc("1", "	dy ve beaghey ayns aggle Yee, as jeaghyn son");

        var result = Query("Aggle");

        Assert.That(result.NumberOfDocuments, Is.EqualTo(1));
    }

    [Test]
    public void SearchMultipleWordsInCorrectOrder()
    {
        this.AddManxDoc("1", 
            "hi",               // no: only matches first
            "hi world",         // yes: perfect match
            "world hi",         // no: wrong order
            "hi not world",     // no: word in-between
            "hi world hi world" // yes x2
        );

        var result = Query("hi world");

        Assert.That(result.NumberOfMatches, Is.EqualTo(3));
        Assert.That(result.NumberOfSegments, Is.EqualTo(2));
    }

    [Test]
    public void TestOr()
    {
        this.AddManxDoc("1",
            "hi",           // 1: matches first
            "hi world",     // 2: perfect match
            "world hi",     // 2: wrong order
            "hi not world", // 2: word in-between
            "worl"          // no:  substring
        );

        var result = Query("hi or world");

        Assert.That(result.NumberOfMatches, Is.EqualTo(7));
        Assert.That(result.NumberOfSegments, Is.EqualTo(4));
    }

    [Test]
    public void TestAnd()
    {
        this.AddManxDoc("1",
            "hi",           // 0: matches first
            "hi world",     // 1: perfect match
            "world hi",     // 1: wrong order
            "hi not world", // 1: word in-between
            "worl"          // no:  substring
        );

        var result = Query("hi and world");

        Assert.That(result.NumberOfMatches, Is.EqualTo(3));
        Assert.That(result.NumberOfSegments, Is.EqualTo(3));
    }

    [Test]
    public void TestNot()
    {
        // Equal to TestAnd
        this.AddManxDoc("1",
            "hi",            // 1: matches first
            "hi world",      // 0: not
            "world hi",      // 0: not
            "hi note world", // 0: word in-between
            "hid"            // 0: sperstring
        );

        var result = Query("hi not world");

        Assert.That(result.NumberOfMatches, Is.EqualTo(1));
        Assert.That(result.NumberOfSegments, Is.EqualTo(1));
    }

    [Test]
    public void TestUselessBrackets()
    {
        // Equal to TestAnd
        this.AddManxDoc("1",
            "hi",           // 0: matches first
            "hi world",     // 1: perfect match
            "world hi",     // 1: wrong order
            "hi not world", // 1: word in-between
            "worl"          // no:  substring
        );

        var result = Query("(hi) and (world)");

        Assert.That(result.NumberOfMatches, Is.EqualTo(3));
        Assert.That(result.NumberOfSegments, Is.EqualTo(3));
    }

    [Test]
    public void DiacriticMatchIsDefault()
    {
        this.AddManxDoc("1",
            "facade",       // 0: matches first
            "façade"        // 1: perfect match
        );

        var result = Query("facade", ScanOptions.Default);
            
        Assert.That(result.NumberOfMatches, Is.EqualTo(2));
    }

    [Test]
    public void TestNonCedillaSearch()
    {
        this.AddManxDoc("1",
            "facade",       // 0: matches first
            "façade"        // 1: perfect match
        );

        var result1 = Query("facade", new ScanOptions { NormalizeDiacritics = true });
        var result2 = Query("façade", new ScanOptions { NormalizeDiacritics = true });

        Assert.That(result1.NumberOfMatches, Is.EqualTo(2));
        Assert.That(result2.NumberOfMatches, Is.EqualTo(2));
    }

    [Test]
    public void TestDiacriticExact()
    {
        this.AddManxDoc("1",
            "facade",
            "façade"
        );

        var withoutDiacritics1 = Query("façade", new ScanOptions { NormalizeDiacritics = false });
        var withoutDiacritics2 = Query("facade", new ScanOptions { NormalizeDiacritics = false });

        Assert.That(withoutDiacritics1.NumberOfMatches, Is.EqualTo(1), "façade should be only match");
        Assert.That(withoutDiacritics2.NumberOfMatches, Is.EqualTo(1), "facade should be only match");
    }

    [Test]
    public void TestCaseSensitiveSearch()
    {
        // #19 - opt-in case-sensitive matching
        this.AddManxDoc("1", "hello", "Hello");

        var lower = Query("hello", new ScanOptions { CaseSensitive = true });
        var caps = Query("Hello", new ScanOptions { CaseSensitive = true });

        Assert.That(lower.NumberOfMatches, Is.EqualTo(1), "'hello' should be the only match");
        Assert.That(caps.NumberOfMatches, Is.EqualTo(1), "'Hello' should be the only match");
    }

    [Test]
    public void TestCaseSensitiveWithDiacritics()
    {
        // case and diacritics are independent axes: 'Chengey' still matches 'Çhengey'
        this.AddManxDoc("1", "Çhengey", "çhengey");

        var caps = Query("Chengey", new ScanOptions { CaseSensitive = true });
        var lower = Query("chengey", new ScanOptions { CaseSensitive = true });

        Assert.That(caps.NumberOfMatches, Is.EqualTo(1), "'Çhengey' should be the only match");
        Assert.That(lower.NumberOfMatches, Is.EqualTo(1), "'çhengey' should be the only match");
    }

    [Test]
    public void TestCaseSensitiveWithExactDiacritics()
    {
        // both options at once: only the identical form matches
        this.AddManxDoc("1", "Çhengey", "Chengey", "çhengey");

        var result = Query("Çhengey", new ScanOptions { CaseSensitive = true, NormalizeDiacritics = false });

        Assert.That(result.NumberOfMatches, Is.EqualTo(1));
    }

    [Test]
    public void TestCaseSensitiveWildcard()
    {
        this.AddManxDoc("1", "Hello", "hello");

        var caps = Query("H*", new ScanOptions { CaseSensitive = true });
        var lower = Query("h*", new ScanOptions { CaseSensitive = true });

        Assert.That(caps.NumberOfMatches, Is.EqualTo(1), "'Hello' should be the only match");
        Assert.That(lower.NumberOfMatches, Is.EqualTo(1), "'hello' should be the only match");
    }

    [Test]
    public void TestCaseSensitivePhrase()
    {
        this.AddManxDoc("1", "Yn Baase Mooar", "yn baase mooar");

        var result = Query("Baase Mooar", new ScanOptions { CaseSensitive = true });

        Assert.That(result.NumberOfMatches, Is.EqualTo(1));
    }

    [Test]
    public void TestCaseSensitiveWithIgnoreHyphens()
    {
        this.AddManxDoc("1", "Lhiam-Lhiat", "lhiam-lhiat");

        var result = Query("Lhiam Lhiat", new ScanOptions { CaseSensitive = true, IgnoreHyphens = true });

        Assert.That(result.NumberOfMatches, Is.EqualTo(1), "'Lhiam-Lhiat' should be the only match");
    }

    [Test]
    public void TestCaseSensitiveEnglish()
    {
        AddDocument("1",
            new Line { Manx = "", English = "The Tongue" },
            new Line { Manx = "", English = "the tongue" });

        var result = Query("Tongue", new ScanOptions { CaseSensitive = true, SearchType = SearchType.English });

        Assert.That(result.NumberOfMatches, Is.EqualTo(1), "'The Tongue' should be the only match");
    }

    [Test]
    public void TestSample()
    {
        this.AddManxDoc("1",
            "Big list of evil strings",
            "list of this is cool"
        );
        this.AddManxDoc("2",
            "maybe not as interesting",
            "this list is still valid,,, and"
        );

        var result = Query("list");

        Assert.That(result.DocumentResults, Has.Count.EqualTo(2));

        {
            var sample = result.DocumentResults[0];
            Assert.That(sample.Ident, Is.EqualTo("1"));
            Assert.That(sample.Sample, Is.EqualTo("Big list of evil strings"));
        }

        {
            var sample = result.DocumentResults[1];
            Assert.That(sample.Ident, Is.EqualTo("2"));
            Assert.That(sample.Sample, Is.EqualTo("this list is still valid,,, and"));
        }
            
    }

    [Test]
    public void TestWildcardAtEnd()
    {
        this.AddManxDoc("1",
            "san andreas",     // exact match
            "sand and tonio",  // matches *
            "sa n an"          // no match: * should not match space
        );

        var result = Query("san*");

        Assert.That(result.NumberOfMatches, Is.EqualTo(2));
        Assert.That(result.NumberOfSegments, Is.EqualTo(2));
    }

    [Test]
    public void TestWildcardInMiddle()
    {
        this.AddManxDoc("1",
            "san andreas",     // 1: exact match with 1 char
            "saan andreas",    // 1: exact match with 2 chars
            "sn",              // 1: no chars
            "sa n an"          // 0: * should not match space
        );

        var result = Query("s*n");

        Assert.That(result.NumberOfMatches, Is.EqualTo(3));
        Assert.That(result.NumberOfSegments, Is.EqualTo(3));
    }

    [Test]
    public void TestWildChar()
    {
        this.AddManxDoc("1",
            "san andreas",     // 1: exact match with 1 char
            "saan andreas",    // 0: 2 chars
            "sn",              // 0: no chars
            "sa n an"          // 0: _ should not match space
        );

        var result = Query("s_n");

        Assert.That(result.NumberOfMatches, Is.EqualTo(1));
        Assert.That(result.NumberOfSegments, Is.EqualTo(1));
    }

    [Test]
    public void TestPlus()
    {
        // one or more characters

        this.AddManxDoc("1",
            "san andreas",     // 1: exact match with 1 char
            "saan andreas",    // 1: 2 chars
            "sn",              // 0: no chars
            "sa n an"          // 0: ? should not match space
        );

        var result = Query("s+n");

        Assert.That(result.NumberOfMatches, Is.EqualTo(2));
        Assert.That(result.NumberOfSegments, Is.EqualTo(2));
    }

    [Test]
    public void TestPlusNoDiacritics()
    {
        // one or more characters

        this.AddManxDoc("1",
            "san andreas",     // 1: exact match with 1 char
            "saan andreas",    // 1: 2 chars
            "sn",              // 0: no chars
            "sa n an"          // 0: ? should not match space
        );

        var result = Query("s+n", new ScanOptions { NormalizeDiacritics = false });

        Assert.That(result.NumberOfMatches, Is.EqualTo(2));
        Assert.That(result.NumberOfSegments, Is.EqualTo(2));
    }

    [Test]
    public void TestDate()
    {
        this.AddManxDoc("1", "data");

        var result = Query("data");

        Assert.That(result.DocumentResults[0].StartDate, Is.EqualTo(DOC_DATE));
        Assert.That(result.DocumentResults[0].EndDate, Is.EqualTo(DOC_DATE));
    }

    [Test]
    public void TestNullDate()
    {
        var docWithNoDate = new TestDocument("1", null);
        AddDocument(docWithNoDate, new Line { Manx = "data", English = "" });

        var result = Query("data");

        Assert.That(result.DocumentResults[0].StartDate, Is.EqualTo(null));
        Assert.That(result.DocumentResults[0].EndDate, Is.EqualTo(null));
    }

    [Test]
    public void TestCount()
    {
        // 1: aigney
        // 2.1: aegey, arrey
        // 2.2: ""
        // 2.3: aigney aigney
        // 3: aigney
        this.AddManxDoc("1", "Cha vel eh laccal gerjagh ta goaill soylley jeh aigney booiagh.");
        this.AddManxDoc("2", "Ayns yn ynnyd shen va thunnag ny hoie guirr, freayl arrey gys yinnagh ny hêin aegey çheet ass ny hoohyn. ", 
            "V’ee goaill toshiaght dy aase skee son va’n feallagh veggey tra liauyr çheet ass ny hoohyn.",
            "aigney aigney"
        );
        this.AddManxDoc("3", "Cha vel eh laccal gerjagh ta goaill soylley jeh aigney booiagh.");

        var result = Query("a*y");

        Assert.That(result.NumberOfDocuments, Is.EqualTo(3));
        Assert.That(result.NumberOfSegments, Is.EqualTo(4));
        Assert.That(result.NumberOfMatches, Is.EqualTo(6));
    }

    [Test]
    public void TestSmartQuotes()
    {
        // on copy/pasting smart quotes, a normal quote should be searched for.
        this.AddManxDoc("1", "t’ayndoo");
        var result = Query("t’ayndoo");
        Assert.That(result.NumberOfDocuments, Is.EqualTo(1));
    }

    [Test]
    public void TestDiacriticsSpacing()
    {
        // aggwish -> 	latin small letter g and a combining breve (̆ )
        this.AddManxDoc("1", "agğwish er y Chiarn");
        var result = Query("agg");
        Assert.That(result.NumberOfDocuments, Is.EqualTo(0));


        var result2 = Query("agğwish");
        Assert.That(result2.NumberOfDocuments, Is.EqualTo(1));


        var result3 = Query("aggwish");
        Assert.That(result3.NumberOfDocuments, Is.EqualTo(1));
    }

    [Test]
    public void TestEscapeQuestionMarks()
    {
        // #15 - Rob used "???" to mark unknown phrases
        // two issues:
        // ??? was stripped as it was a non-token character
        // ??? matched everything

        // To fix this: 
        // * use _ as the single character wildcard
        // * Don't strip multiple instances of '??'

        this.AddManxDoc("1", "???", "and");

        var result = Query(@"???");

        Assert.That(result.NumberOfSegments, Is.EqualTo(1));
    }

    [Test]
    public void TrailingFullStopIsSkipped()
    {
        // #237 - a search ending in a full stop returned no results
        this.AddManxDoc("1", "T'eh cair da'n slane shiaghtin ve shiaght shamyryn currit da jee.");

        var singleWord = Query("jee.");
        var phrase = Query("currit da jee.");

        Assert.That(singleWord.NumberOfMatches, Is.EqualTo(1), "could not find 'jee.'");
        Assert.That(phrase.NumberOfMatches, Is.EqualTo(1), "could not find 'currit da jee.'");
    }

    [Test]
    public void TrailingPunctuationIsSkipped()
    {
        // #237 - same as the full stop: ',', ';' and '!' are all replaced with spaces on the query
        this.AddManxDoc("1", "currit da jee");

        Assert.That(Query("jee,").NumberOfMatches, Is.EqualTo(1), "could not find 'jee,'");
        Assert.That(Query("jee;").NumberOfMatches, Is.EqualTo(1), "could not find 'jee;'");
        Assert.That(Query("jee!").NumberOfMatches, Is.EqualTo(1), "could not find 'jee!'");
    }

    [Test]
    public void TrailingQuestionMarkIsSkipped()
    {
        // #15 - We want to search for '???'
        // ??? was stripped as it was a non-token character
        // So, we want to keep that, but we do want to strip a trailing '?'

        this.AddManxDoc("1", "erbee", "erbee?");

        var result = Query(@"erbee");

        Assert.That(result.NumberOfSegments, Is.EqualTo(2));
    }

    [Test]
    public void TrailingDoubleQuestionMarkIsNotSkipped()
    {
        // #15 - We want to search for '???'
        // ??? was stripped as it was a non-token character
        // So, we want to keep that, but we do want to strip a trailing '?'

        this.AddManxDoc("1", "erbee??");

        var result = Query(@"erbee");

        Assert.That(result.NumberOfSegments, Is.EqualTo(1));
    }


    [Test, Ignore("Fails: Converted to a dash, which is a valid character")]
    public void TestQuestionMarkInstance()
    {
        this.AddManxDoc("1", "da?—Cre");

        var resultDa = Query(@"da");
        var resultCre = Query(@"cre");
        var resultQuestion = Query(@"*?*");

        Assert.That(resultDa.NumberOfSegments, Is.EqualTo(1));
        Assert.That(resultCre.NumberOfSegments, Is.EqualTo(1));
        Assert.That(resultQuestion.NumberOfSegments, Is.EqualTo(0));
    }

    [Test]
    public void TestDashes()
    {
        this.AddManxDoc("1", "da cre-erbee");

        var resultDa = Query(@"da");
        var resultCre = Query(@"cre");
        var resultCreErbee = Query(@"cre-erbee");

        Assert.That(resultDa.NumberOfSegments, Is.EqualTo(1), "could not find 'da'");
        Assert.That(resultCre.NumberOfSegments, Is.EqualTo(1), "could not find 'cre'");
        Assert.That(resultCreErbee.NumberOfSegments, Is.EqualTo(1), "could not find 'cre-erbee'");
    }

    [Test]
    public void TestIgnoreHyphensWithHyphenatedQuery()
    {
        // #18 - hyphens, spaces and joined words are interchangeable
        this.AddManxDoc("1", "lhiam-lhiat", "lhiam lhiat", "lhiamlhiat");

        var withOption = Query("lhiam-lhiat", new ScanOptions { IgnoreHyphens = true });
        var withoutOption = Query("lhiam-lhiat");

        Assert.That(withOption.NumberOfSegments, Is.EqualTo(3), "should match all three forms");
        Assert.That(withoutOption.NumberOfSegments, Is.EqualTo(1), "by default, only the hyphenated form should match");
    }

    [Test]
    public void TestIgnoreHyphensWithSpacedQuery()
    {
        // #18 - a space-separated query should also match the hyphenated/joined forms
        this.AddManxDoc("1", "lhiam-lhiat", "lhiam lhiat", "lhiamlhiat");

        var withOption = Query("lhiam lhiat", new ScanOptions { IgnoreHyphens = true });
        var withoutOption = Query("lhiam lhiat");

        Assert.That(withOption.NumberOfSegments, Is.EqualTo(3), "should match all three forms");
        Assert.That(withoutOption.NumberOfSegments, Is.EqualTo(1), "by default, only the spaced form should match");
    }

    [Test]
    public void TestIgnoreHyphensWithJoinedQuery()
    {
        // a joined query cannot know where a space would fall, so 'lhiam lhiat' is not matched
        this.AddManxDoc("1", "lhiam-lhiat", "lhiam lhiat", "lhiamlhiat");

        var withOption = Query("lhiamlhiat", new ScanOptions { IgnoreHyphens = true });
        var withoutOption = Query("lhiamlhiat");

        Assert.That(withOption.NumberOfSegments, Is.EqualTo(2), "should match the hyphenated and joined forms");
        Assert.That(withoutOption.NumberOfSegments, Is.EqualTo(1), "by default, only the joined form should match");
    }

    [Test]
    public void TestIgnoreHyphensWithinPhrase()
    {
        // the hyphenated word can sit inside a longer phrase
        this.AddManxDoc("1", "yn lhiam-lhiat mooar", "yn lhiam lhiat mooar", "yn lhiamlhiat mooar");

        var result = Query("yn lhiam lhiat mooar", new ScanOptions { IgnoreHyphens = true });

        Assert.That(result.NumberOfSegments, Is.EqualTo(3));
    }

    [Test]
    public void TestIgnoreHyphensWithDiacritics()
    {
        // IgnoreHyphens composes with diacritic normalization
        this.AddManxDoc("1", "çhione-jiarg");

        var result = Query("chione jiarg", new ScanOptions { IgnoreHyphens = true });

        Assert.That(result.NumberOfSegments, Is.EqualTo(1));
    }

    [Test]
    public void TestIgnoreHyphensWithThreePartWord()
    {
        // all mixed groupings of a three-part word match
        this.AddManxDoc("1", "cur-my-ner", "cur my ner", "curmyner", "cur-my ner", "cur my-ner");

        var result = Query("cur my ner", new ScanOptions { IgnoreHyphens = true });

        Assert.That(result.NumberOfSegments, Is.EqualTo(5));
    }

    [Test]
    public void TestIgnoreHyphensWithHyphenRuns()
    {
        // a run of hyphens (e.g. an ASCII em-dash: 'a---b') is still 'a hyphen'
        this.AddManxDoc("1", "cur---my");

        Assert.That(Query("cur---my", new ScanOptions { IgnoreHyphens = true }).NumberOfSegments,
            Is.EqualTo(1), "could not find 'cur---my' with its own text");
        Assert.That(Query("cur-my", new ScanOptions { IgnoreHyphens = true }).NumberOfSegments,
            Is.EqualTo(1), "could not find 'cur---my' with 'cur-my'");
        Assert.That(Query("cur my", new ScanOptions { IgnoreHyphens = true }).NumberOfSegments,
            Is.EqualTo(1), "could not find 'cur---my' with 'cur my'");
        Assert.That(Query("curmy", new ScanOptions { IgnoreHyphens = true }).NumberOfSegments,
            Is.EqualTo(1), "could not find 'cur---my' with 'curmy'");
    }

    [Test]
    public void TestIgnoreHyphensWithLeadingHyphen()
    {
        // dialogue dashes: '—Cre' is indexed as the single token '-cre'
        this.AddManxDoc("1", "—Cre t’ou");

        var result = Query("cre", new ScanOptions { IgnoreHyphens = true });

        Assert.That(result.NumberOfSegments, Is.EqualTo(1));
    }

    [Test]
    public void TestIgnoreHyphensWithEnDash()
    {
        // '–' is normalized to '-' on both the index and the query
        this.AddManxDoc("1", "lhiam–lhiat");

        var result = Query("lhiam lhiat", new ScanOptions { IgnoreHyphens = true });

        Assert.That(result.NumberOfSegments, Is.EqualTo(1));
    }
        
    [Test]
    public void OrPrefix()
    {
        // searching for 'orrym' failed as it started with 'or'
        this.AddManxDoc("1", "Ta'n ennym orrym David");

        var result = Query("orrym");

        Assert.That(result.NumberOfMatches, Is.EqualTo(1));
        Assert.That(result.NumberOfSegments, Is.EqualTo(1));
    }

    private ScanResult Query(string query, ScanOptions options)
    {
        return new Searcher(luceneIndex, parser).Scan(query, options);
    }

    private ScanResult Query(string query)
    {
        return Query(query, ScanOptions.Default);
    }
}