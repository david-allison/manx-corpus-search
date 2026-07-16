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

    /// <summary>An affix is attested by the words carrying it: 'aa-' by
    /// 'aa-vioghey', which is the only way a prefix ever is said</summary>
    [Test]
    public void AnAffixIsAttestedByTheWordsCarryingIt()
    {
        var vocabulary = Loaded("aa-vioghey", "cooinaghtyn");

        Assert.Multiple(() =>
        {
            Assert.That(vocabulary.IsAttested("aa-"), Is.True);
            // no text carries this one
            Assert.That(vocabulary.IsAttested("neu-"), Is.False);
        });
    }

    /// <summary>...and never by the bare word it is spelled like. The corpus says
    /// 'an' 126 times meaning *their* (Phillips writes "’an"), and none of it is
    /// the prefix 'an-'. Every path below this drops the hyphen that tells the two
    /// apart, so the affix has to be caught before they do.</summary>
    [Test]
    public void AnAffixIsNotAttestedByTheWordItIsSpelledLike()
    {
        var vocabulary = Loaded("an", "aa");

        Assert.Multiple(() =>
        {
            Assert.That(vocabulary.IsAttested("an-"), Is.False);
            Assert.That(vocabulary.IsAttested("aa-"), Is.False);
            // the word itself is still a word
            Assert.That(vocabulary.IsAttested("an"), Is.True);
        });
    }

    /// <summary>A suffix, which the books print the other way round</summary>
    [Test]
    public void ASuffixIsAttestedByTheWordsItEnds()
    {
        var vocabulary = Loaded("shirveish-ys", "ys");

        Assert.Multiple(() =>
        {
            Assert.That(vocabulary.IsAttested("-ys"), Is.True);
            Assert.That(vocabulary.IsAttested("-agh"), Is.False);
        });
    }

    /// <summary>A hyphen inside a word is not an affix marker: 'aa-aase' is a word
    /// the corpus can perfectly well say</summary>
    [Test]
    public void AWordWithAHyphenInsideItIsNotAnAffix()
    {
        Assert.That(Loaded("aa-aase").IsAttested("aa-aase"), Is.True);
    }
}
