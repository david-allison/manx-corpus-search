using CorpusSearch.Service;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// The dictionary headwords that are not words — 'an-', 'aa-', '-ys' — and what
/// the corpus should be asked about them.
/// </summary>
[TestFixture]
public class AffixTest
{
    /// <summary>Told by its hyphen, and there is nothing else to tell it by:
    /// NormalizeForm folds 'an-' and 'an' into one key</summary>
    [Test]
    public void AnAffixIsToldByItsHyphen()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Affix.Is("an-"), Is.True);
            Assert.That(Affix.Is("-ys"), Is.True);
            // the books print the non-breaking hyphen too, and mean the same
            Assert.That(Affix.Is("‑YS"), Is.True);
            Assert.That(Affix.Is("aa- "), Is.True);

            Assert.That(Affix.Is("an"), Is.False);
            // a hyphen inside a word joins it, it does not cut it loose
            Assert.That(Affix.Is("aa-aase"), Is.False);
            Assert.That(Affix.Is("-"), Is.False);
            Assert.That(Affix.Is(""), Is.False);
        });
    }

    /// <summary>An affix is attested by the words carrying it, so that is what is
    /// asked for. Its own hyphen stays in the query — the tokenizer keeps a hyphen
    /// inside the token it joins, so 'aa-vioghey' is one word beginning "aa-" —
    /// and only the affix's outer edge is open.</summary>
    [Test]
    public void ThePrefixAsksForTheWordsItBegins()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Affix.CorpusQuery("an-"), Is.EqualTo("an-*"));
            Assert.That(Affix.CorpusQuery("aa- "), Is.EqualTo("aa-*"));
        });
    }

    [Test]
    public void TheSuffixAsksForTheWordsItEnds()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Affix.CorpusQuery("-agh"), Is.EqualTo("*-agh"));
            // the non-breaking hyphen is not what is indexed
            Assert.That(Affix.CorpusQuery("‑YS"), Is.EqualTo("*-YS"));
        });
    }
}
