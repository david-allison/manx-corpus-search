using System.Linq;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Service;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// What the corpus actually says, as against what a dictionary lists it could
/// say: the difference a browse greys out.
/// </summary>
[TestFixture]
public class CorpusVocabularyTest
{
    private static CorpusVocabulary Loaded(params string[] terms)
    {
        var vocabulary = new CorpusVocabulary(LemmaTable.Instance);
        vocabulary.Init(terms.Select(t => (t, 1L)));
        return vocabulary;
    }

    [Test]
    public void AWordTheCorpusUsesIsAttested()
    {
        Assert.That(Loaded("jaagh").IsAttested("jaagh"), Is.True);
    }

    [Test]
    public void AWordNoTextSaysIsNot()
    {
        Assert.That(Loaded("jaagh").IsAttested("jaagheyder"), Is.False);
    }

    /// <summary>The index holds the analyzer's spelling, so the headword has to be
    /// folded the same way before it can be found</summary>
    [Test]
    public void TheHeadwordIsFoldedTheWayTheIndexIs()
    {
        Assert.That(Loaded("cheusthie").IsAttested("Cheusthie"), Is.True);
    }

    /// <summary>The corpus indexes words, not phrases: every word of a headword
    /// being used is the most that can honestly be claimed for it</summary>
    [Test]
    public void APhraseIsMetOneWordAtATime()
    {
        var vocabulary = Loaded("cur", "my", "ner");

        Assert.Multiple(() =>
        {
            Assert.That(vocabulary.IsAttested("cur my ner"), Is.True);
            Assert.That(vocabulary.IsAttested("cur my sooill"), Is.False);
        });
    }

    /// <summary>'yaagh' is 'jaagh' after a lenition: a text saying the one attests
    /// the other, and the lemma table is what knows so</summary>
    [Test]
    public void AWordTheCorpusOnlyWritesMutatedIsStillAttested()
    {
        Assert.That(Loaded("jaagh").IsAttested("yaagh"), Is.True);
    }

    /// <summary>Before the index loads there is no evidence either way, and a
    /// browse would rather say nothing than grey the whole language out</summary>
    [Test]
    public void NothingIsGreyedBeforeTheIndexLoads()
    {
        var vocabulary = new CorpusVocabulary(LemmaTable.Instance);

        Assert.That(vocabulary.IsAttested("jaagheyder"), Is.True);
    }
}
