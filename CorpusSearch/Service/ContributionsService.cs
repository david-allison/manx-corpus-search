using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CorpusSearch.Model;
using CorpusSearch.Services;
using Newtonsoft.Json.Linq;

namespace CorpusSearch.Service;

/// <summary>
/// The all-time contributions leaderboard (#347), from two per-document sources:
/// free-form metadata keys ("translated by", "Transcriber", ...) and the sections
/// of license.txt ("## Transcription\nRob Teare 2023") — the licenses attribute
/// work which predates the metadata keys.
/// "author" is deliberately not a contribution: it names the (usually historical)
/// writer of the text, not work done on the corpus.
/// </summary>
public class ContributionsService(WorkService workService)
{
    /// <summary>
    /// Manifest keys which credit a contributor, keyed with everything but letters
    /// removed. A value crediting several people in different roles ("R. Teare,
    /// proofread by M. Wheeler" under "transcription") credits them all with the
    /// key's role: an accepted approximation.
    /// </summary>
    private static readonly Dictionary<string, string[]> RolesByNormalizedKey = new()
    {
        ["translator"] = ["Translation"],
        ["translation"] = ["Translation"],
        ["translated"] = ["Translation"],
        ["translatedby"] = ["Translation"],
        ["transcriber"] = ["Transcription"],
        ["transcription"] = ["Transcription"],
        ["transcribed"] = ["Transcription"],
        ["transcribedby"] = ["Transcription"],
        ["transcriptiontranslation"] = ["Transcription", "Translation"],
        ["transcribedtranslated"] = ["Transcription", "Translation"],
        ["transliterationby"] = ["Transcription"],
        ["proofread"] = ["Proofreading"],
        ["proofreadby"] = ["Proofreading"],
        ["proofreader"] = ["Proofreading"],
        ["corrected"] = ["Proofreading"],
        ["editor"] = ["Editing"],
        ["edited"] = ["Editing"],
        ["editedby"] = ["Editing"],
        ["editing"] = ["Editing"],
        ["revised"] = ["Editing"],
        ["standardisedmanx"] = ["Standardisation"],
        ["standardisedmanxby"] = ["Standardisation"],
        ["standardisation"] = ["Standardisation"],
        ["standardisedby"] = ["Standardisation"],
        ["digitisation"] = ["Digitisation"],
        ["digitised"] = ["Digitisation"],
        ["digitisedby"] = ["Digitisation"],
        ["digitizedby"] = ["Digitisation"],
        // from the git author of the commit adding document.csv (add_upload_credits)
        ["uploadedby"] = ["Upload"],
    };

    /// <summary>
    /// One person is credited under initials and name variants: map each to a
    /// canonical name. Self-entries ("Phil Kelly") exist so a person is recognised
    /// inside a compound value ("Google Translate / Phil Kelly") via <see cref="AliasPatterns"/>.
    /// </summary>
    private static readonly Dictionary<string, string> CanonicalNameByAlias = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RT"] = "Rob Teare",
        ["RT."] = "Rob Teare",
        ["R. Teare"] = "Rob Teare",
        ["R Teare"] = "Rob Teare",
        ["R.Teare"] = "Rob Teare",
        ["Rob Teare"] = "Rob Teare",
        ["Robert Teare"] = "Rob Teare",
        ["Max Wheeler"] = "Max Wheeler",
        ["Max W. Wheeler"] = "Max Wheeler",
        ["M. Wheeler"] = "Max Wheeler",
        ["Christopher Lewin"] = "Christopher Lewin",
        ["Christoper Lewin"] = "Christopher Lewin", // typo in the data
        ["C. Lewin"] = "Christopher Lewin",
        // surname-only, as cited in "Broderick (1981), Lewin (2014)"
        ["Lewin"] = "Christopher Lewin",
        ["Broderick"] = "George Broderick",
        ["George Broderick"] = "George Broderick",
        ["Paul Rogers"] = "Paul Rogers",
        ["Phil Kelly"] = "Phil Kelly",
        ["Phil Key"] = "Phil Kelly", // typo in the data
        ["Chris Sheard"] = "Chris Sheard",
        ["C. Sheard"] = "Chris Sheard",
        ["C.Sheard"] = "Chris Sheard",
        ["Walter Clarke"] = "Walter Clarke",
        ["Walter Clarke, Ramsey"] = "Walter Clarke",
        ["Fiona McArdle"] = "Fiona McArdle",
        ["Fiona McArdle, Kirk Michael"] = "Fiona McArdle",
        ["E. Faragher"] = "Edward Faragher",
        ["Morrison. S"] = "S. Morrison",
        ["J J Kneen"] = "J.J. Kneen",
        ["J. Gell"] = "John Gell",
    };

    /// <summary>
    /// Credited in manifests, but not contributors to the corpus: the authors
    /// and translators of the source texts, publications, and their-era pseudonyms.
    /// They remain visible on each document's metadata page; the leaderboard is for
    /// the volunteers who put texts into the corpus.
    /// </summary>
    private static readonly HashSet<string> ExcludedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Edward Faragher",
        "S. Morrison",
        "Morrison. L.",
        "A.W. Moore",
        "Dr. John Clague",
        "The Rev. Hugh Stowell of Ballaugh",
        "H. Stowell",
        "William Crebbin", // 1762
        "J. Train",
        "C. Vallency",
        "J.J. Kneen",
        "John Gell",
        "Wm. Radcliffe",
        "Isle of Man Examiner",
        "IOM Times",
        "Manx Language Society",
        "DYNYSS",
        "J.R.M.",
    };

    /// <summary>
    /// Aliases as whole-word patterns, for values an alias is embedded in, e.g.
    /// "English; RT. Manx; Not yet". Case-sensitive: lower-case "rt" occurs in prose.
    /// </summary>
    private static readonly List<(Regex Pattern, string CanonicalName)> AliasPatterns =
        CanonicalNameByAlias.Select(alias =>
            (new Regex($@"\b{Regex.Escape(alias.Key)}\b"), alias.Value)).ToList();

    /// <summary>
    /// Values recording absence or uncertainty rather than a person ("unknown",
    /// "Not yet", "Unspecified, likely Walter Clarke, Ramsey"): uncertain
    /// attributions credit nobody, matching how document pages show them verbatim.
    /// </summary>
    private static readonly Regex NonName = new(@"^(unknown|not yet|n/a|none|anonymous|unspecified)\b", RegexOptions.IgnoreCase);

    /// <summary>Whole values which are prose about the text, not a credit</summary>
    private static readonly HashSet<string> IgnoredValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "accompanies original",
        "in original",
        "authors",
        // towns, left over from "Max W. Wheeler\nRamsey, August 2021"-style credits
        "Ramsey",
        "Kirk Michael",
    };

    /// <summary>A role label left over after splitting: "Transcribed &amp; Translated; R.Teare"</summary>
    private static readonly Regex RoleWord = new(
        @"^(transcribed|transcription|translated|translation|proofread(ing)?|edited|editing|standardis(ed|ation))$",
        RegexOptions.IgnoreCase);

    /// <summary>A qualifier after the name: "R. Teare (suggested translation)"</summary>
    private static readonly Regex TrailingParenthetical = new(@"\s*\([^)]*\)\s*$");

    /// <summary>A year after the name: "Rob Teare 2021", "William Crebbin 1762"</summary>
    private static readonly Regex TrailingYear = new(@"\s+(1[6-9]|20)\d\d$");

    private static readonly Regex NonLetters = new("[^a-z]");

    /// <summary>Longer values are citations or attribution notes, not names</summary>
    private const int MaxNameLength = 40;

    /// <summary>Boilerplate in license sections which never names a contributor</summary>
    private static readonly Regex LicenseNoise = new(
        @"public domain|published before|licen[cs]ed?|creative commons|all rights|document provided",
        RegexOptions.IgnoreCase);

    /// <summary>A line which is only a date: the credit is on the line above it</summary>
    private static readonly Regex LicenseDateOnly = new(
        @"^((january|february|march|april|may|june|july|august|september|october|november|december),?\s*)?(1[6-9]|20)\d\d$",
        RegexOptions.IgnoreCase);

    /// <summary>"Copyright (c) 2021 Rob Teare" attributes the work to the holder</summary>
    private static readonly Regex LicenseCopyright = new(@"^copyright \(c\)\s*(19|20)\d\d\s+", RegexOptions.IgnoreCase);

    /// <summary>"By Rob Teare 2025", "With thanks to Tim Swales 2025"</summary>
    private static readonly Regex LicenseAttributionPrefix = new(@"^(by|with thanks to)\s+", RegexOptions.IgnoreCase);

    /// <summary>"Max Wheeler August 2021", "Max W. Wheeler August, 2021"</summary>
    private static readonly Regex LicenseTrailingDate = new(
        @"\s+((january|february|march|april|may|june|july|august|september|october|november|december),?\s+)?(1[6-9]|20)\d\d$",
        RegexOptions.IgnoreCase);

    /// <summary>
    /// Computed once: the documents don't change after startup, and this reads
    /// every document's license file. A concurrent first request may compute it
    /// twice; both results are identical.
    /// </summary>
    private volatile List<Contributor>? cache;

    /// <summary>The leaderboard: contributors ordered by number of documents credited</summary>
    public async Task<List<Contributor>> GetContributors()
    {
        if (cache != null) return cache;
        var documents = await workService.GetAll();

        // contributor name -> document ident -> the document's name and credited roles
        var byContributor = new Dictionary<string, Dictionary<string, (string DocumentName, SortedSet<string> Roles)>>();

        void Credit(IDocument document, string value, string[] roles)
        {
            foreach (var name in ExtractNames(value))
            {
                if (!byContributor.TryGetValue(name, out var contributorDocuments))
                {
                    contributorDocuments = byContributor[name] = [];
                }
                if (!contributorDocuments.TryGetValue(document.Ident, out var entry))
                {
                    entry = contributorDocuments[document.Ident] = (document.Name, new SortedSet<string>(StringComparer.Ordinal));
                }
                entry.Roles.UnionWith(roles);
            }
        }

        foreach (var document in documents)
        {
            foreach (var (key, rawValue) in document.GetAllExtensionData())
            {
                if (!RolesByNormalizedKey.TryGetValue(NormalizeKey(key), out var roles)) continue;
                if (AsString(rawValue) is not { } value) continue;
                Credit(document, value, roles);
            }

            if (document is OpenSourceDocument { LocationOnDisk: not null } onDisk && File.Exists(onDisk.LicenseLink))
            {
                var license = await File.ReadAllTextAsync(onDisk.LicenseLink);
                foreach (var (roles, value) in ParseLicenseCredits(license))
                {
                    Credit(document, value, roles);
                }
            }
        }

        return cache = byContributor
            .Select(contributor => new Contributor(
                Name: contributor.Key,
                DocumentCount: contributor.Value.Count,
                Roles: contributor.Value.Values
                    .SelectMany(x => x.Roles)
                    .GroupBy(x => x)
                    .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Count()),
                Documents: contributor.Value
                    .OrderBy(x => x.Value.DocumentName, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new ContributedDocument(x.Key, x.Value.DocumentName, x.Value.Roles.ToList()))
                    .ToList()))
            .OrderByDescending(x => x.DocumentCount)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeKey(string key) => NonLetters.Replace(key.ToLowerInvariant(), "");

    /// <summary>Extension data values are strings or Newtonsoft tokens, depending on how the document was built</summary>
    private static string? AsString(object? value) => value switch
    {
        string s => s,
        JValue { Type: JTokenType.String } token => (string?)token.Value,
        _ => null,
    };

    /// <summary>The corpus contributors named by one metadata value</summary>
    internal static IEnumerable<string> ExtractNames(string value) =>
        ExtractCandidateNames(value).Where(name => !ExcludedNames.Contains(name));

    private static IEnumerable<string> ExtractCandidateNames(string value)
    {
        if (IgnoredValues.Contains(value.Trim())) yield break;

        // "Morrison. S & Morrison. L." credits two people
        foreach (var part in value.Split('&'))
        {
            var name = TrailingParenthetical.Replace(part, "").TrimEnd();
            name = TrailingYear.Replace(name, "").Trim().TrimEnd(',');
            // a sentence-ending period ("S. Morrison.") splits one person into two
            // spellings; a period closing an initial ("J.R.M.", "Morrison. L.") stays
            if (name.EndsWith('.') && name.Length >= 2 && char.IsLower(name[^2]))
            {
                name = name[..^1];
            }
            if (name.Length == 0 || NonName.IsMatch(name) || IgnoredValues.Contains(name)) continue;

            if (CanonicalNameByAlias.TryGetValue(name, out var canonical))
            {
                yield return canonical;
                continue;
            }

            // Compound values ("Paul Rogers, Christopher Lewin", "Manx to English:
            // R.Teare (...)") credit each person recognised by a known alias.
            var aliased = false;
            foreach (var (pattern, aliasCanonical) in AliasPatterns)
            {
                if (!pattern.IsMatch(name)) continue;
                aliased = true;
                yield return aliasCanonical;
            }
            if (aliased) continue;

            // What remains with list/sentence structure, or too long to be a name,
            // is prose about the text; so is a bare role label left by splitting.
            if (name.Contains(';') || name.Contains(':') || name.Length > MaxNameLength) continue;
            if (RoleWord.IsMatch(name)) continue;

            yield return name;
        }
    }

    /// <summary>
    /// Credits from a license.txt: "## section" headers describe the work
    /// ("## Transcription", "## English"), the lines beneath attribute it
    /// ("Rob Teare 2023", "Copyright (c) 2021 Rob Teare"). "## Original ..."
    /// sections describe the source text, not corpus work.
    /// </summary>
    internal static IEnumerable<(string[] Roles, string Value)> ParseLicenseCredits(string licenseText)
    {
        var roles = Array.Empty<string>();
        foreach (var rawLine in licenseText.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("## "))
            {
                roles = RolesForLicenseSection(line[3..]);
                continue;
            }
            if (roles.Length == 0 || line.Length == 0) continue;

            if (LicenseCopyright.IsMatch(line))
            {
                yield return (roles, LicenseCopyright.Replace(line, ""));
                continue;
            }
            if (LicenseNoise.IsMatch(line) || LicenseDateOnly.IsMatch(line)) continue;

            var value = LicenseAttributionPrefix.Replace(line, "");
            value = LicenseTrailingDate.Replace(value, "");
            if (value.Length > 0) yield return (roles, value);
        }
    }

    private static string[] RolesForLicenseSection(string header)
    {
        if (header.Contains("original", StringComparison.OrdinalIgnoreCase)) return [];
        var roles = new List<string>();
        if (header.Contains("transcription", StringComparison.OrdinalIgnoreCase)) roles.Add("Transcription");
        if (header.Contains("english", StringComparison.OrdinalIgnoreCase)
            || header.Contains("translation", StringComparison.OrdinalIgnoreCase)) roles.Add("Translation");
        if (header.Contains("correct", StringComparison.OrdinalIgnoreCase)) roles.Add("Proofreading");
        // the video documents' transcript section
        if (header.Trim().Equals("manx", StringComparison.OrdinalIgnoreCase)) roles.Add("Transcription");
        return [.. roles];
    }

    /// <summary>
    /// Roles and per-document credits are tracked internally (and kept correct by
    /// tests), but the API publishes only name and rank — see ContributionsController.
    /// </summary>
    public record Contributor(string Name, int DocumentCount, Dictionary<string, int> Roles, List<ContributedDocument> Documents);

    public record ContributedDocument(string Ident, string Name, List<string> Roles);
}
