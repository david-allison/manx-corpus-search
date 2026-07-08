using System.Threading.Tasks;
using CorpusSearch.Controllers;
using CorpusSearch.Model;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace CorpusSearch.Test;

[TestFixture]
public class SearchControllerTest
{
    private static readonly string MaxLengthQuery = new('a', CorpusSearchQuery.MAX_LENGTH);
    private static readonly string TooLongQuery = new('a', CorpusSearchQuery.MAX_LENGTH + 1);

    /// <remarks>Services are not used here.</remarks>
    private static SearchController GetController() => new(null, null, [], null);

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
}
