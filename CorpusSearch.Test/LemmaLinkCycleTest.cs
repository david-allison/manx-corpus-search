using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace CorpusSearch.Test;

/// <summary>
/// The lemma tables' link graph must not grow cycles: a reader following
/// root links from a word must never be walked in a circle (e -> eh -> e).
/// Runs over the vendored tables, like <see cref="UdLemmaAgreementTest"/>.
/// </summary>
[TestFixture]
public class LemmaLinkCycleTest
{
    private record Row(string Form, string LemmaId, string Display, string LinkType);

    /// <summary>Directed cycles the print itself creates: fee (v. to weave)
    /// genuinely inflects to feeagh, and feeagh (s. m. a raven) genuinely
    /// pluralizes to fee. Form-level links cannot express that these are
    /// different readings; anything new here needs the same justification.</summary>
    private static readonly HashSet<string> BookTrueDirectedCycles =
    [
        "fee -> feeagh -> fee",
    ];

    /// <summary>Variant links joining two self-standing headwords: each makes
    /// the dictionary bounce between two pages that claim one another (the
    /// e/eh report). Reviewed pairs live here; fixing one in cregeen-nvh means
    /// deleting its line. New unreviewed pairs fail the test.</summary>
    private static readonly HashSet<string> ReviewedHeadwordVariantPairs =
    [
        "aarlee -> aarl",
        "bannee -> bann",
        "be -> bey",
        "blayst -> blass",
        "boirey -> boiraghey",
        "booaliught -> bhullught",
        "brasnee -> brasn",
        "breag -> breig",
        "breg -> breig",
        "brynnyragh -> brynnagh",
        "cast -> carit",
        "ceabbagh -> cabbagh",
        "cha gred -> creid",
        "chagred -> creid",
        "choodee -> chood",
        "choyrlee -> choyrl",
        "chuirr -> chuir",
        "cleaynee -> cleayn",
        "cleiee -> cleaiee",
        "cleiy -> cleigh",
        "cloghey -> clo",
        "coair -> coayr",
        "combaase -> combaas",
        "connaasagh -> connysson",
        "cooinaght -> cooinaghtyn",
        "corrag -> currag",
        "coyrlee -> coyrl",
        "craidoilagh -> craideyder",
        "creoi -> creogh",
        "croink -> crink",
        "cron -> croan",
        "cronk -> crank",
        "croym -> croymm",
        "cuirt -> cuirrit",
        "currym -> curm",
        "custhee -> custh",
        "daney -> daaney",
        "daunse -> dauns",
        "deyree -> deyr",
        "dhaill -> daill",
        "dhoan -> doyn",
        "dooshtey -> doosht",
        "driaght -> driagh",
        "druight -> gruight",
        "eabb -> eab",
        "eaynnee -> eannee",
        "eeassee -> eeass",
        "eeym -> eem",
        "eh -> e",
        "eit -> eieit",
        "enn -> enney",
        "ennee -> enney",
        "erbe -> erbey",
        "faarkee -> faark",
        "famlee -> faml",
        "feer chorrym -> feer chorm",
        "feerchorrym -> feer chorm",
        "fenee -> fen",
        "fer choadee -> coadeyder",
        "ferchoadee -> coadeyder",
        "firriney -> firrin",
        "fockl -> fockl",
        "gerjoil -> gerjoilid",
        "gerrit -> gerrid",
        "giallee -> giall",
        "gilchreest -> custal",
        "goarley -> gorley",
        "hann -> thannee",
        "jeeig -> jeeg",
        "oaye -> oaie",
        "ogh -> ugh",
        "osnee -> osn",
        "raaue -> raau",
        "rowl -> roll",
        "ruggyr -> rugg",
        "saill -> sahll",
        "sassey -> aashagh",
        "scape -> scap",
        "shickyree -> shickyr",
        "shooit -> shuit",
        "shymlee -> shyml",
        "skyrr -> skir",
        "sniem -> sniemm",
        "snuig -> snog",
        "soie -> soi",
        "soill -> soaill",
        "soilley -> soailley",
        "sonnish -> sannish",
        "sooree -> soor",
        "soylee -> soyl",
        "sthap -> sthapp",
        "streiu -> streeu",
        "struge -> strug",
        "surr -> sur",
        "taa -> taah",
        "taaue -> taau",
        "toallee -> thollee",
        "toarrey -> toar",
        "toiggalagh -> toiggaltagh",
        "traie -> traih",
        "treisht -> treishteil",
        "vagh -> beagh",
        "verr -> behr",
        "yeean -> eean",
        "yial -> giallee",
        "ymmylt -> ymmilt",
        "yn chrow -> chrou",
        "yn veyr -> bayr",
        "ynchrow -> chrou",
        "ynsee -> yns",
        "ynveyr -> bayr",
    ];

    private static List<Row> LoadRows()
    {
        var rows = new List<Row>();
        foreach (var name in new[] { "cregeen.tsv", "names.tsv", "phillips.tsv", "vocab.tsv" })
        {
            var path = Startup.GetLocalFile("Resources", name);
            if (!File.Exists(path))
            {
                continue;
            }
            foreach (var line in File.ReadLines(path).Skip(1))
            {
                var c = line.Split('\t');
                if (c.Length >= 4 && c[0].Length > 0)
                {
                    rows.Add(new Row(c[0], c[1], c[2].ToLowerInvariant(), c[3]));
                }
            }
        }
        Assert.That(rows, Is.Not.Empty, "no vendored tables found (is the submodule initialised?)");
        return rows;
    }

    [Test]
    public void RootLinksHaveNoNewDirectedCycles()
    {
        var edges = new Dictionary<string, HashSet<string>>();
        foreach (var row in LoadRows().Where(r => r.LinkType is not ("self" or "demutated")))
        {
            if (row.Display.Length > 0 && row.Display != row.Form)
            {
                (edges.TryGetValue(row.Form, out var s) ? s : edges[row.Form] = []).Add(row.Display);
            }
        }

        var cycles = new HashSet<string>();
        var color = new Dictionary<string, int>(); // 0 white, 1 grey, 2 black
        var stack = new List<string>();
        void Visit(string node)
        {
            color[node] = 1;
            stack.Add(node);
            foreach (var next in edges.TryGetValue(node, out var s) ? s : [])
            {
                var state = color.GetValueOrDefault(next);
                if (state == 1)
                {
                    // canonical signature: rotate to the smallest member, so
                    // the DFS entry point cannot rename a known cycle
                    var members = stack.Skip(stack.IndexOf(next)).ToList();
                    var start = members.IndexOf(members.Min(StringComparer.Ordinal)!);
                    var rotated = members.Skip(start).Concat(members.Take(start)).ToList();
                    cycles.Add(string.Join(" -> ", rotated.Append(rotated[0])));
                }
                else if (state == 0)
                {
                    Visit(next);
                }
            }
            stack.RemoveAt(stack.Count - 1);
            color[node] = 2;
        }
        foreach (var node in edges.Keys.ToList())
        {
            if (color.GetValueOrDefault(node) == 0)
            {
                Visit(node);
            }
        }

        var unexpected = cycles.Where(c => !BookTrueDirectedCycles.Contains(c)).ToList();
        Assert.That(unexpected, Is.Empty,
            "new directed link cycles (a reader is walked in a circle):\n" + string.Join("\n", unexpected));
    }

    [Test]
    public void HeadwordVariantPairsAreAllReviewed()
    {
        var rows = LoadRows();
        var selfIdsByForm = rows.Where(r => r.LinkType == "self")
            .GroupBy(r => r.Form)
            .ToDictionary(g => g.Key, g => g.Select(r => r.LemmaId).ToHashSet());

        var pairs = rows
            .Where(r => r.LinkType == "variant")
            .Where(r => selfIdsByForm.ContainsKey(r.Form)
                        && selfIdsByForm.ContainsKey(r.Display)
                        && !selfIdsByForm[r.Form].Contains(r.LemmaId))
            .Select(r => $"{r.Form} -> {r.Display}")
            .ToHashSet();

        var unreviewed = pairs.Except(ReviewedHeadwordVariantPairs).Order().ToList();
        Assert.That(unreviewed, Is.Empty,
            "variant links joining two self-standing headwords need review " +
            "(each bounces the dictionary between two pages):\n" + string.Join("\n", unreviewed));
    }
}
