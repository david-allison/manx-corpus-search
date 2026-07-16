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

    /// <summary>
    /// A phrase is not said by a text saying its words apart: 'aachummey eddin'
    /// was called attested on the strength of 'aachummey' in one line and 'eddin'
    /// in another, and its page then had nothing to show. Half of Phil Kelly is
    /// phrases, so the corpus is read for them once and each is answered from that.
    /// </summary>
    [Test]
    public void APhraseIsAttestedOnlyWhereALineSaysIt()
    {
        var vocabulary = Loaded("aachummey", "eddin", "cur", "my", "ner");
        vocabulary.ScanPhrases(
            ["aachummey eddin", "cur my ner"],
            ["Ta mee cur my ner yn eddin", "as aachummey ny lurg shen"]);

        Assert.Multiple(() =>
        {
            // said, in that order, in one line
            Assert.That(vocabulary.IsAttested("cur my ner"), Is.True);
            // both words are used, but no line says the phrase
            Assert.That(vocabulary.IsAttested("aachummey eddin"), Is.False);
            // and its words are still words
            Assert.That(vocabulary.IsAttested("eddin"), Is.True);
        });
    }

    /// <summary>The pass reads the corpus behind the running server, and until it
    /// lands there is no answer: the page says so rather than claiming either way,
    /// and a browse index of ten thousand headwords greys nothing it has not
    /// read</summary>
    [Test]
    public void APhraseHasNoAnswerUntilTheCorpusHasBeenReadForIt()
    {
        var vocabulary = Loaded("aachummey", "eddin");

        Assert.Multiple(() =>
        {
            Assert.That(vocabulary.Attestation("aachummey eddin"), Is.Null);
            // ...and the index leaves it alone rather than greying a guess
            Assert.That(vocabulary.IsAttested("aachummey eddin"), Is.True);
            // a single word never waits: the term list already answers it
            Assert.That(vocabulary.Attestation("eddin"), Is.True);
        });
    }

    /// <summary>The phrase must be met whole and in order, not merely word by word
    /// within one line</summary>
    [Test]
    public void APhraseIsNotSaidByItsWordsSharingALine()
    {
        var vocabulary = Loaded("cur", "my", "ner");
        vocabulary.ScanPhrases(["cur my ner"], ["ner as my chur, cur ny lurg"]);

        Assert.That(vocabulary.IsAttested("cur my ner"), Is.False);
    }

    /// <summary>A phrase is read the way the term list is, so a line's punctuation
    /// and case cannot hide it</summary>
    [Test]
    public void APhraseIsFoundThroughTheLinesPunctuation()
    {
        var vocabulary = Loaded("cur", "my", "ner");
        vocabulary.ScanPhrases(["Cur my ner"], ["Eisht, cur my ner: hie eh."]);

        Assert.That(vocabulary.IsAttested("cur my ner"), Is.True);
    }

    /// <summary>Single headwords go nowhere near the pass: the term list has
    /// already answered them, and carrying 34,000 of them would only slow it</summary>
    [Test]
    public void ASingleHeadwordIsNotScannedAsAPhrase()
    {
        var vocabulary = Loaded("jaagh");
        vocabulary.ScanPhrases(["jaagh", "cur my ner"], ["ta jaagh ayn"]);

        Assert.Multiple(() =>
        {
            Assert.That(vocabulary.IsAttested("jaagh"), Is.True);
            Assert.That(vocabulary.IsAttested("cur my ner"), Is.False);
        });
    }

    /// <summary>The lemma tree asks for the spelling itself: 'yaagh' attests the
    /// lexeme (<see cref="AWordTheCorpusOnlyWritesMutatedIsStillAttested"/>), but
    /// it does not attest the spelling 'yaagh' being asked about the other way —
    /// the hop would answer for the whole paradigm at once</summary>
    [Test]
    public void AFormIsAttestedByItsOwnSpellingNotItsParadigm()
    {
        var vocabulary = Loaded("jaagh");

        Assert.Multiple(() =>
        {
            Assert.That(vocabulary.AttestsForm("jaagh"), Is.True);
            Assert.That(vocabulary.AttestsForm("yaagh"), Is.False);
        });
    }

    /// <summary>The lemma table spaces a hyphenated form ('aa-vioghey' is 'aa
    /// vioghey' there), so the corpus is folded the same way before the two meet</summary>
    [Test]
    public void ASpacedFormIsSaidByItsHyphenatedToken()
    {
        Assert.That(Loaded("aa-vioghey").AttestsForm("aa vioghey"), Is.True);
    }

    /// <summary>A spaced form of several words is a phrase: read for, not guessed
    /// at, and unanswered until the read lands</summary>
    [Test]
    public void APhraseFormWaitsForTheCorpusToBeRead()
    {
        var vocabulary = Loaded("er", "n'aase", "ta", "mee");
        Assert.That(vocabulary.AttestsForm("er n'aase"), Is.Null);

        vocabulary.ScanPhrases(["er n'aase"], ["ta mee er n'aase"]);
        Assert.That(vocabulary.AttestsForm("er n'aase"), Is.True);
    }

    [Test]
    public void NoFormIsGreyedBeforeTheIndexLoads()
    {
        Assert.That(new CorpusVocabulary(LemmaTable.Instance).AttestsForm("xyzzy"), Is.True);
    }
}
