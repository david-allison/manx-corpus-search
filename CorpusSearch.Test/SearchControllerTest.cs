using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CorpusSearch.Controllers;
using CorpusSearch.Model;
using CorpusSearch.Service;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace CorpusSearch.Test;

[TestFixture]
public class SearchControllerTest
{
    private static readonly string MaxLengthQuery = new('a', CorpusSearchQuery.MAX_LENGTH);
    private static readonly string TooLongQuery = new('a', CorpusSearchQuery.MAX_LENGTH + 1);

    /// <remarks>Services are not used here.</remarks>
    private static SearchController GetController() => new(null!, null!, [], null!);

    [Test]
    public async Task SearchCorpusRejectsTooLongQuery()
    {
        var result = await GetController().SearchCorpus(TooLongQuery);

        Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        Assert.That(result.Value, Is.Null);
    }

    [Test]
    public async Task SearchWorkRejectsTooLongQuery()
    {
        var result = await GetController().SearchWork("anyIdent", TooLongQuery);

        Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        Assert.That(result.Value, Is.Null);
    }

    [Test]
    public void QueryAtMaxLengthIsValid()
    {
        var query = new CorpusSearchQuery(MaxLengthQuery) { Manx = true };
        Assert.That(query.IsValid(), Is.True);
    }

    [Test]
    public void QueryOverMaxLengthIsInvalid()
    {
        var query = new CorpusSearchQuery(TooLongQuery) { Manx = true };
        Assert.That(query.IsValid(), Is.False);
    }

    [Test]
    public void WorkQueryAtMaxLengthIsValid()
    {
        var query = new CorpusSearchWorkQuery(MaxLengthQuery) { Ident = "anyIdent", Manx = true };
        Assert.That(query.IsValid(), Is.True);
    }

    [Test]
    public void WorkQueryOverMaxLengthIsInvalid()
    {
        var query = new CorpusSearchWorkQuery(TooLongQuery) { Ident = "anyIdent", Manx = true };
        Assert.That(query.IsValid(), Is.False);
    }

    // #150: a Manx query mapped to "en", so a Manx-only search hit the English dictionaries
    [Test]
    public void QueryLanguagesManxMapsToGv()
    {
        Assert.That(new QueryLanguages(Manx: true, English: false).AsList(), Is.EqualTo(new[] { "gv" }));
        Assert.That(new QueryLanguages(Manx: false, English: true).AsList(), Is.EqualTo(new[] { "en" }));
        Assert.That(new QueryLanguages(Manx: true, English: true).AsList(), Is.EquivalentTo(new[] { "en", "gv" }));
        Assert.That(new QueryLanguages(Manx: false, English: false).AsList(), Is.Empty);
    }

    [Test]
    public void ManxOnlyLookupQueriesOnlyManxDictionaries()
    {
        var result = DictionaryLookup(new QueryLanguages(Manx: true, English: false));
        Assert.That(result.Keys, Is.EquivalentTo(new[] { "manxDictionary" }));
    }

    [Test]
    public void EnglishOnlyLookupQueriesOnlyEnglishDictionaries()
    {
        var result = DictionaryLookup(new QueryLanguages(Manx: false, English: true));
        Assert.That(result.Keys, Is.EquivalentTo(new[] { "englishDictionary" }));
    }

    [Test]
    public void BilingualLookupQueriesAllDictionaries()
    {
        var result = DictionaryLookup(new QueryLanguages(Manx: true, English: true));
        Assert.That(result.Keys, Is.EquivalentTo(new[] { "manxDictionary", "englishDictionary" }));
    }

    // #159: 'aall ' returned no translations, as the dictionaries perform exact-match lookups
    [Test]
    public void TranslationsFromManxTrimsQuery()
    {
        Startup.ManxToEnglishDictionary = new Dictionary<string, IList<string>> { ["aall"] = new List<string> { "fork" } };

        var result = SearchController.Translations.FromManx("aall ");

        Assert.That(result["Phil Kelly (en)"], Is.EqualTo(new[] { "fork" }));
    }

    [Test]
    public void TranslationsFromEnglishTrimsQuery()
    {
        Startup.EnglishToManxDictionary = new Dictionary<string, IList<string>> { ["fork"] = new List<string> { "aall" } };

        var result = SearchController.Translations.FromEnglish(" fork");

        Assert.That(result["Phil Kelly (gv)"], Is.EqualTo(new[] { "aall" }));
    }

    [Test]
    public void DictionaryLookupTrimsQuery()
    {
        var result = DictionaryLookup(new QueryLanguages(Manx: true, English: false), "moddey ");

        Assert.That(result["manxDictionary"].Entries, Is.EqualTo(new[] { "definition of moddey" }));
    }

    private static Dictionary<string, SearchController.DictionaryData> DictionaryLookup(QueryLanguages languages, string query = "moddey")
    {
        ISearchDictionary[] dictionaries = [new FakeDictionary("manxDictionary", "gv"), new FakeDictionary("englishDictionary", "en")];
        var controller = new SearchController(null!, null!, dictionaries, null!);
        return controller.DictionaryLookup(query, languages);
    }

    private class FakeDictionary(string identifier, params string[] queryLanguages) : ISearchDictionary
    {
        public string Identifier => identifier;
        public List<string> QueryLanguages => queryLanguages.ToList();
        public bool LinkToDictionary => false;
        public IEnumerable<DictionarySummary> GetSummaries(string query, bool basic = false) =>
            [new DictionarySummary { Summary = $"definition of {query}", PrimaryWord = query }];
    }
}
