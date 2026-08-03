using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Services;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>Token-weighted disambiguation coverage of the ambiguous corpus
/// mass: how much of it the equivalences, the overrides and the sidecar
/// actually settle, and what remains untouched — the companion measurement to
/// <see cref="CorpusAmbiguityAnalysis"/>, re-run as adjudication progresses.
///
/// Run: dotnet test CorpusSearch.Test --filter FullyQualifiedName~CorpusDisambiguationCoverage --logger "console;verbosity=detailed"
/// </summary>
[TestFixture]
[Explicit("one-off analysis over the full corpus, not a regression test")]
public class CorpusDisambiguationCoverage
{
    [Test]
    public void Report()
    {
        var table = LemmaTable.Instance;
        var resolver = LemmaResolver.Instance;
        Assert.That(table.FormCount, Is.GreaterThan(39_000), "vendored table missing");
        Assert.That(resolver.HasSidecarRows, "sidecar missing");

        var documents = OpenDataLoader.LoadDocumentsFromFile(null)
            .Concat(ClosedDataLoader.LoadDocumentsFromFile())
            .ToList();

        long tokens = 0, covered = 0, ambiguous = 0;
        long equivalenceOnly = 0, overridePinned = 0, overrideNarrowed = 0;
        long sidecarIndex = 0, sidecarPopup = 0, sidecarNarrowed = 0, untouched = 0;
        var untouchedForms = new Dictionary<string, int>();
        var failed = 0;

        foreach (var document in documents)
        {
            List<CorpusSearch.Model.DocumentLine> lines;
            try
            {
                lines = document.LoadPreparedLines();
            }
            catch (Exception)
            {
                failed++;
                continue;
            }
            foreach (var line in lines.Where(x => x.IsManxLanguage))
            {
                var stream = LemmaResolver.TokenizeManx(line.NormalizedManx);
                var key = LemmaResolver.LineKey(stream);
                for (var i = 0; i < stream.Count; i++)
                {
                    var token = stream[i];
                    tokens++;
                    var candidates = table.CandidatesFor(token);
                    if (candidates.Count == 0)
                    {
                        candidates = table.CliticCandidatesFor(token);
                    }
                    if (candidates.Count == 0)
                    {
                        continue;
                    }
                    covered++;
                    if (candidates.Count < 2)
                    {
                        continue;
                    }
                    ambiguous++;
                    if (resolver.SameLexeme(candidates))
                    {
                        equivalenceOnly++;
                        continue;
                    }
                    var over = resolver.OverrideFor(token);
                    if (over != null)
                    {
                        if (over.Count == 1 || resolver.SameLexeme(over))
                        {
                            overridePinned++;
                        }
                        else
                        {
                            overrideNarrowed++;
                        }
                        continue;
                    }
                    var index = resolver.SidecarFor(key, i, token, includePopupTier: false);
                    var any = index ?? resolver.SidecarFor(key, i, token, includePopupTier: true);
                    if (any != null)
                    {
                        if (any.Count == 1 || resolver.SameLexeme(any))
                        {
                            if (index != null)
                            {
                                sidecarIndex++;
                            }
                            else
                            {
                                sidecarPopup++;
                            }
                        }
                        else
                        {
                            sidecarNarrowed++;
                        }
                        continue;
                    }
                    untouched++;
                    untouchedForms[token] = untouchedForms.GetValueOrDefault(token) + 1;
                }
            }
        }

        var settled = equivalenceOnly + overridePinned + sidecarIndex + sidecarPopup;
        var report = new StringBuilder();
        report.AppendLine($"documents: {documents.Count} ({failed} failed); gv tokens: {tokens:N0}; covered: {covered:N0}");
        report.AppendLine($"ambiguous (>=2 candidate ids): {ambiguous:N0} = {ambiguous / (double)covered:P2} of covered");
        report.AppendLine();
        report.AppendLine($"  equivalence collapses (same-lexeme ids): {equivalenceOnly:N0} ({equivalenceOnly / (double)ambiguous:P1})");
        report.AppendLine($"  override-pinned (form-level):            {overridePinned:N0} ({overridePinned / (double)ambiguous:P1})");
        report.AppendLine($"  override-narrowed (still >1 lexeme):     {overrideNarrowed:N0} ({overrideNarrowed / (double)ambiguous:P1})");
        report.AppendLine($"  sidecar-settled, index tier:             {sidecarIndex:N0} ({sidecarIndex / (double)ambiguous:P1})");
        report.AppendLine($"  sidecar-settled, popup tier:             {sidecarPopup:N0} ({sidecarPopup / (double)ambiguous:P1})");
        report.AppendLine($"  sidecar-narrowed (still >1 lexeme):      {sidecarNarrowed:N0} ({sidecarNarrowed / (double)ambiguous:P1})");
        report.AppendLine($"  untouched:                               {untouched:N0} ({untouched / (double)ambiguous:P1})");
        report.AppendLine();
        report.AppendLine($"settled to one lexeme: {settled:N0} = {settled / (double)ambiguous:P1} of ambiguous mass");
        report.AppendLine();
        report.AppendLine("top 25 untouched forms:");
        foreach (var (form, count) in untouchedForms.OrderByDescending(x => x.Value).Take(25))
        {
            report.AppendLine($"  {count,7:N0}  {form,-16} [{string.Join(", ", table.CandidatesFor(form))}]");
        }
        TestContext.Progress.WriteLine(report.ToString());
    }
}
