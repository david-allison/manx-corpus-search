using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CorpusSearch.Dependencies;
using CorpusSearch.Model;
using CorpusSearch.Service;
using CorpusSearch.Test.TestUtils;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>#158: 'did you mean' candidates for a query which found nothing</summary>
[TestFixture]
public class HyphenSuggestionTest : QueryBase
{
    [Test]
    public void FusedQuerySuggestsHyphenatedTerm()
    {
        this.AddManxDoc("1", "yn lum-lane mooar");

        var alternates = Alternates("lumlane");

        Assert.That(alternates, Does.Contain("lum-lane"));
    }

    [Test]
    public void HyphenatedQuerySuggestsJoinedAndSpacedForms()
    {
        this.AddManxDoc("1", "lhiamlhiat");

        var alternates = Alternates("lhiam-lhiat");

        Assert.That(alternates, Does.Contain("lhiamlhiat"));
        Assert.That(alternates, Does.Contain("lhiam lhiat"));
    }

    [Test]
    public void SpacedQuerySuggestsHyphenatedForm()
    {
        this.AddManxDoc("1", "yn lum-lane mooar");

        var alternates = Alternates("lum lane");

        Assert.That(alternates, Does.Contain("lum-lane"));
        Assert.That(alternates, Does.Not.Contain("lum lane"), "the query itself is not an alternative");
    }

    [Test]
    public void TheQueryItselfIsNotSuggested()
    {
        this.AddManxDoc("1", "yn lum-lane mooar");

        Assert.That(Alternates("lum-lane"), Does.Not.Contain("lum-lane"));
    }

    [Test]
    public void WildcardQueriesHaveNoSuggestions()
    {
        this.AddManxDoc("1", "yn lum-lane mooar");

        Assert.That(Alternates("lum*lane"), Is.Empty);
        Assert.That(Alternates("lumlane*"), Is.Empty);
    }

    [Test]
    public void SuggestionsRespectDiacritics()
    {
        this.AddManxDoc("1", "çhione-jiarg");

        Assert.That(Alternates("chionejiarg"), Does.Contain("çhione-jiarg"));
    }

    [Test]
    public async Task SuggestionsAreCountedViaTheRealSearch()
    {
        this.AddManxDoc("1", "yn lum-lane mooar", "as lum-lane elley");

        var suggestions = await GetSuggestions("lumlane");

        Assert.That(suggestions, Is.EqualTo(new List<SearchSuggestion> { new("lum-lane", 2) }));
    }

    [Test]
    public async Task SuggestionCountsRespectTheDateRange()
    {
        this.AddManxDoc("1", "yn lum-lane mooar");

        var suggestions = await GetSuggestions("lumlane", maxDate: DOC_DATE.AddYears(-1));

        Assert.That(suggestions, Is.Empty, "matches outside the date range should not be suggested");
    }

    private async Task<List<SearchSuggestion>> GetSuggestions(string query, DateTime? maxDate = null)
    {
        var workService = new WorkService();
        workService.AddWork(new TestDocument("1", DOC_DATE));
        var service = new OverviewSearchService2(workService, new Searcher(luceneIndex, parser));

        return await service.GetSuggestions(new CorpusSearchQuery(query)
        {
            Manx = true,
            MinDate = DateTime.MinValue,
            MaxDate = maxDate ?? DateTime.MaxValue,
        });
    }

    private List<string> Alternates(string query)
    {
        return new Searcher(luceneIndex, parser).GetHyphenAlternates(query, SearchOptions.Default);
    }
}
