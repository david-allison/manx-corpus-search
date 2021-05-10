using CorpusSearch.Dependencies;
using CorpusSearch.Model;
using CorpusSearch.Test.TestUtils;
using NUnit.Framework;
using static CorpusSearch.Test.TestUtils.DocumentStorageExtensions;

namespace CorpusSearch.Test
{
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
            // TODO: We will fix this later once I write an optional case sensitive span query, but for now, this works.
            this.AddManxDoc("1", "Hello");

            var result = Query("hello");

            Assert.That(result.NumberOfMatches, Is.EqualTo(1));
            Assert.That(result.NumberOfSegments, Is.EqualTo(1));
        }

        [Test]
        public void TestSearchForCaps()
        {
            // TODO: We will fix this later once I write an optional case sensitive span query, but for now, this works.
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
               "sa n an"          // 0: ? should not match space
            );

            var result = Query("s?n");

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


        private ScanResult Query(string query, ScanOptions options)
        {
            return new Searcher(luceneIndex, parser).Scan(query, options);
        }

        private ScanResult Query(string query)
        {
            return Query(query, ScanOptions.Default);
        }
    }
}
