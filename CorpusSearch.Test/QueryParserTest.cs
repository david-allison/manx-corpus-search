using Codex_API.Dependencies;
using Codex_API.Model;
using NUnit.Framework;
using static Codex_API.Test.TestUtils.DocumentStorageExtensions;

namespace Codex_API.Test
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

            Assert.Inconclusive("not implemented");

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
            // TODO: I think this wants to be like a modified WildcardQuery - match all like the wildcards?

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
