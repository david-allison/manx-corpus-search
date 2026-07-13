using System;
using System.Linq;
using CorpusSearch.Controllers;
using CorpusSearch.Service.Dictionaries;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// The Culture Vannin spoken dictionary: entries resolve by word (phrases
/// with their punctuation stripped), every summary carries the recording's
/// URL and the source's URL, and the identifier cites the source.
/// </summary>
[TestFixture]
public class CultureVanninSpokenDictionaryServiceTest
{
    private static CultureVanninSpokenDictionaryService Service()
    {
        return new CultureVanninSpokenDictionaryService(new CultureVanninSpokenDictionaryService.SpokenArtifact
        {
            Name = "LearnManx Spoken Dictionary",
            Credit = "Spoken Dictionary",
            Url = "https://www.learnmanx.com/learning/spoken-dictionary/",
            Entries =
            [
                new CultureVanninSpokenDictionaryService.SpokenEntry
                {
                    Word = "moddey", Translation = "dog",
                    AudioUrl = "https://www.learnmanx.com/media/x.mp3", Topic = "animals",
                },
                new CultureVanninSpokenDictionaryService.SpokenEntry
                {
                    Word = "Kys t'ou?", Translation = "How are you?",
                    AudioUrl = "https://www.learnmanx.com/media/y.mp3", Topic = "how-are-you",
                },
                new CultureVanninSpokenDictionaryService.SpokenEntry
                {
                    Word = "Baldrine", Translation = "",
                    AudioUrl = "https://www.learnmanx.com/media/z.mp3", Topic = "pocket-guide-manx-place-names",
                },
            ],
        });
    }

    [Test]
    public void AWordResolvesWithAudioAndCitation()
    {
        var summary = Service().GetSummaries("Moddey").Single();

        Assert.Multiple(() =>
        {
            Assert.That(summary.PrimaryWord, Is.EqualTo("moddey"));
            Assert.That(summary.Summary, Is.EqualTo("dog"));
            Assert.That(summary.AudioUrl,
                Is.EqualTo("/api/Audio?url=" + Uri.EscapeDataString("https://www.learnmanx.com/media/x.mp3")));
            Assert.That(summary.SourceUrl, Is.EqualTo("https://www.learnmanx.com/learning/spoken-dictionary/"));
            Assert.That(summary.SourceCredit, Is.EqualTo("Spoken Dictionary"));
        });
    }

    [Test]
    public void APhraseResolvesWithoutItsPunctuation()
    {
        // the tap/context pipeline strips '?': the recorded phrase still matches
        Assert.That(Service().GetSummaries("kys t'ou").Single().PrimaryWord, Is.EqualTo("Kys t'ou?"));
    }

    [Test]
    public void AGlosslessEntryStillAnswers()
    {
        Assert.That(Service().GetSummaries("baldrine").Single().Summary, Is.EqualTo("pronunciation"));
    }

    [Test]
    public void TheIdentifierCitesTheSource()
    {
        var service = Service();
        Assert.That(service.Identifier, Is.EqualTo("LearnManx Spoken Dictionary"));
        Assert.That(service.ContainsWord("moddey"), Is.True);
        Assert.That(service.AllWords, Is.EquivalentTo(new[] { "moddey", "Kys t'ou?", "Baldrine" }));
    }

    /// <summary>The relay is not an open proxy: only the source's own mp3s</summary>
    [Test]
    public void TheAudioRelayOnlyAcceptsTheSourcesMedia()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AudioController.AllowedUrl.IsMatch(
                "https://www.learnmanx.com/media/Dictionary%20-%20Animals/Moddey.mp3"), Is.True);
            Assert.That(AudioController.AllowedUrl.IsMatch(
                "http://www.learnmanx.com/media/x.mp3"), Is.False); // https only
            Assert.That(AudioController.AllowedUrl.IsMatch(
                "https://evil.example/media/x.mp3"), Is.False);
            Assert.That(AudioController.AllowedUrl.IsMatch(
                "https://www.learnmanx.com/media/x.mp3?next=https://evil.example"), Is.False);
            Assert.That(AudioController.AllowedUrl.IsMatch(
                "https://www.learnmanx.com/other/x.mp3"), Is.False);
            Assert.That(AudioController.AllowedUrl.IsMatch(
                "https://www.learnmanx.com/media/x.exe"), Is.False);
        });
    }
}
