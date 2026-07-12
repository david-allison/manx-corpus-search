using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Services;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Util;
using Newtonsoft.Json;
using NUnit.Framework;

namespace CorpusSearch.Test.LemmaAdjudication;

public sealed record UdWord(string Form, string Lemma);

public sealed record UdSentence(string? TextEn, List<UdWord> Words, bool IsTestFile, string? Text = null);

/// <summary>
/// Shared plumbing for the lemma disambiguation artifacts: the sidecar
/// generator (gloss scorer) and the LLM adjudication exporter/importer all
/// read the same table, treebank, glosses and corpus through these helpers.
/// </summary>
public static class AdjudicationCommon
{
    /// <summary>Lemma comparison key: normalized with hyphens/spaces collapsed away,
    /// so the table's "neu-ghlen" matches UD's "neughlen"</summary>
    public static string DisplayKey(string lemma)
    {
        return LemmaTable.NormalizeForm(lemma).Replace(" ", "");
    }

    /// <summary>lemma id -> display lemma, straight from the vendored table's columns</summary>
    public static Dictionary<string, string> DisplayLemmaById()
    {
        var result = new Dictionary<string, string>();
        foreach (var columns in TableRows())
        {
            result.TryAdd(columns[1], columns[2]);
        }
        return result;
    }

    /// <summary>(normalized form, lemma id) -> link types, for provenance in prompts
    /// ("demutated" tells the adjudicator the reading is a mutation guess)</summary>
    public static Dictionary<(string Form, string Id), string> LinkTypesByFormId()
    {
        var result = new Dictionary<(string, string), string>();
        foreach (var columns in TableRows())
        {
            var key = (columns[0], columns[1]);
            var linkType = columns.Length > 3 ? columns[3] : "self";
            if (result.TryGetValue(key, out var existing))
            {
                if (!existing.Split(',').Contains(linkType))
                {
                    result[key] = existing + "," + linkType;
                }
            }
            else
            {
                result[key] = linkType;
            }
        }
        return result;
    }

    private static IEnumerable<string[]> TableRows()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Resources", "cregeen.tsv");
        foreach (var line in File.ReadLines(path).Skip(1))
        {
            var columns = line.Split('\t');
            if (columns.Length >= 3)
            {
                yield return columns;
            }
        }
    }

    /// <summary>display lemma (normalized) -> gloss word set from manx.json,
    /// with s-stripped variants for light stemming</summary>
    public static Dictionary<string, HashSet<string>> LoadGlosses()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Resources", "manx.json");
        var dictionary = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("manx.json deserialized to null");
        var result = new Dictionary<string, HashSet<string>>();
        foreach (var (headword, senses) in dictionary)
        {
            var key = LemmaTable.NormalizeForm(headword);
            if (!result.TryGetValue(key, out var words))
            {
                result[key] = words = [];
            }
            foreach (var word in senses.SelectMany(EnglishWords))
            {
                words.Add(word);
            }
        }
        return result;
    }

    /// <summary>display lemma (normalized) -> raw gloss strings from manx.json,
    /// for human/LLM-readable candidate descriptions</summary>
    public static Dictionary<string, List<string>> LoadGlossTexts()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Resources", "manx.json");
        var dictionary = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("manx.json deserialized to null");
        var result = new Dictionary<string, List<string>>();
        foreach (var (headword, senses) in dictionary)
        {
            var key = LemmaTable.NormalizeForm(headword);
            if (!result.TryGetValue(key, out var texts))
            {
                result[key] = texts = [];
            }
            foreach (var sense in senses)
            {
                if (!string.IsNullOrWhiteSpace(sense) && texts.Count < 6)
                {
                    texts.Add(sense.Trim());
                }
            }
        }
        return result;
    }

    public static IEnumerable<string> EnglishWords(string text)
    {
        var word = new StringBuilder();
        foreach (var c in text.ToLowerInvariant() + " ")
        {
            if (char.IsLetter(c))
            {
                word.Append(c);
                continue;
            }
            if (word.Length == 0)
            {
                continue;
            }
            var value = word.ToString();
            word.Clear();
            yield return value;
            if (value.Length > 3 && value.EndsWith('s'))
            {
                yield return value[..^1];
            }
        }
    }

    public static void ForEachManxLine(List<OpenSourceDocument> documents,
        Action<string, CorpusSearch.Model.DocumentLine> action)
    {
        foreach (var document in documents)
        {
            List<CorpusSearch.Model.DocumentLine> lines;
            try
            {
                lines = document.LoadPreparedLines();
            }
            catch (Exception)
            {
                continue;
            }
            foreach (var line in lines.Where(x => x.IsManxLanguage))
            {
                action(document.Ident, line);
            }
        }
    }

    public static List<UdSentence> TreebankSentences()
    {
        var directory = Path.Combine(TestContext.CurrentContext.TestDirectory, "Resources", "UD_Manx-Cadhan");
        var files = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.conllu") : [];
        Assert.That(files, Is.Not.Empty, "treebank missing: git submodule update --init && rebuild");

        var sentences = new List<UdSentence>();
        foreach (var file in files.OrderBy(x => x))
        {
            var isTestFile = Path.GetFileName(file).Contains("-test");
            var current = new UdSentence(null, [], isTestFile);
            foreach (var line in File.ReadLines(file).Append(""))
            {
                if (line.Length == 0)
                {
                    if (current.Words.Count > 0)
                    {
                        sentences.Add(current);
                    }
                    current = new UdSentence(null, [], isTestFile);
                    continue;
                }
                if (line.StartsWith("# text_en = "))
                {
                    current = current with { TextEn = line["# text_en = ".Length..] };
                    continue;
                }
                if (line.StartsWith("# text = "))
                {
                    current = current with { Text = line["# text = ".Length..] };
                    continue;
                }
                if (line[0] == '#')
                {
                    continue;
                }
                var columns = line.Split('\t');
                if (columns.Length < 4 || columns[0].Contains('-') || columns[0].Contains('.'))
                {
                    continue;
                }
                if (columns[3] is "PUNCT" or "NUM" or "X")
                {
                    continue;
                }
                current.Words.Add(new UdWord(columns[1], columns[2]));
            }
        }
        return sentences;
    }

    /// <summary>form -> every UD-attested reading (display keys). A resolution
    /// must never drop an attested reading; trainOnly keeps the veto off the
    /// eval's own labels.</summary>
    public static Dictionary<string, HashSet<string>> AttestedReadings(
        IEnumerable<UdSentence> sentences, bool trainOnly)
    {
        var result = new Dictionary<string, HashSet<string>>();
        foreach (var sentence in sentences.Where(x => !trainOnly || !x.IsTestFile))
        {
            foreach (var word in sentence.Words)
            {
                var form = LemmaTable.NormalizeForm(word.Form);
                if (!result.TryGetValue(form, out var readings))
                {
                    result[form] = readings = [];
                }
                readings.Add(DisplayKey(word.Lemma));
            }
        }
        return result;
    }

    public static string Hash(string text)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
    }

    /// <summary>The sidecar line key: a hash of the normalized token stream, so
    /// punctuation/case/apostrophe-style edits to the cell can't orphan rows,
    /// while any token change correctly invalidates them.</summary>
    public static string LineKey(string manxCell)
    {
        return Hash(string.Join(" ", Tokenize(manxCell)));
    }

    /// <summary>The manx_lemma pipeline's tokens: uncased ManxTokenizer + ManxTokenFilter</summary>
    public static List<string> Tokenize(string text)
    {
        var result = new List<string>();
        var tokenizer = new ManxTokenizer(LuceneVersion.LUCENE_48, new StringReader(text));
        using var stream = new ManxTokenFilter(tokenizer);
        var term = stream.GetAttribute<ICharTermAttribute>();
        stream.Reset();
        while (stream.IncrementToken())
        {
            result.Add(term.ToString());
        }
        stream.End();
        return result;
    }

    /// <summary>Reads a form-level overrides file (lemma.overrides[.seed].tsv):
    /// form -> resolved lemma ids. Comment lines (#) and the header are skipped.</summary>
    public static Dictionary<string, string[]> LoadOverrides(string path)
    {
        var result = new Dictionary<string, string[]>();
        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith('#') || line.StartsWith("form\t") || string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            var columns = line.Split('\t');
            if (columns.Length >= 2)
            {
                result[columns[0]] = columns[1].Split(',');
            }
        }
        return result;
    }
}
