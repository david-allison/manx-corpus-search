using CorpusSearch.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CorpusSearch.Controllers;
using CorpusSearch.Infrastructure;
using CorpusSearch.Model;
using CorpusSearch.Dependencies.csly;
using CorpusSearch.Dependencies;
using CorpusSearch.Dependencies.Lucene;
using CorpusSearch.Service;
using CorpusSearch.Service.Dictionaries;
using Microsoft.Extensions.Logging;
using static System.Text.Json.JsonSerializer;

namespace CorpusSearch;

public class LoadConfig
{
    //add below to appsettings.Development.json for fast load
    //},
    //"Loading": {
    //    "OpenDataOnly":  true,
    //    "VideoOnly": true,
    //    "MaxOpenData": 1
    //}
    public bool VideoOnly { get; set;}
    public bool OpenDataOnly { get; set;}
    public int MaxOpenData { get; set;}
    /// <summary>Overrides the directory documents are loaded from (e2e tests use a fixture corpus)</summary>
    public string? OpenDataPath { get; set; }
    //public LoadConfig(bool videoOnlyConfig) => videoOnlyConfig = videoOnly;
}

public class Startup(IConfiguration configuration)
{
    // set by SetupDictionaries before the server starts serving
    public static Dictionary<string, IList<string>> EnglishToManxDictionary { get; set; } = null!;
    public static Dictionary<string, IList<string>> ManxToEnglishDictionary { get; set; } = null!;


    public IConfiguration Configuration { get; } = configuration;

    // assigned at the start of Configure, before its callees use it
    private ILogger<Startup> log = null!;

    // Dev only: the Vite dev server the SPA middleware proxies to (and launches).
    private static readonly Uri ViteDevServerUrl = new("http://localhost:3000");

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllersWithViews().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });
        // the pronunciation relay (AudioController) fetches from learnmanx.com
        services.AddHttpClient();

        services.AddSingleton(provider => LuceneIndex.GetInstance());
        services.AddSingleton(provider => SearchParser.GetParser());
        services.AddSingleton<Searcher>();
        services.AddSingleton(provider => CregeenDictionaryService.Init(provider.GetRequiredService<ILogger<CregeenDictionaryService>>()));
        services.AddSingleton<ISearchDictionary>(provider => provider.GetRequiredService<CregeenDictionaryService>());
        services.AddSingleton(provider => KellyManxToEnglishDictionaryService.Init(provider.GetRequiredService<ILogger<KellyManxToEnglishDictionaryService>>()));
        services.AddSingleton<ISearchDictionary>(provider => provider.GetRequiredService<KellyManxToEnglishDictionaryService>());
        if (CultureVanninSpokenDictionaryService.Enabled)
        {
            services.AddSingleton(provider => CultureVanninSpokenDictionaryService.Init(provider.GetRequiredService<ILogger<CultureVanninSpokenDictionaryService>>()));
            services.AddSingleton<ISearchDictionary>(provider => provider.GetRequiredService<CultureVanninSpokenDictionaryService>());
        }
        // resolves lazily on first lookup, after SetupDictionaries has loaded manx.json
        services.AddSingleton(provider => PhilKellyDictionaryService.Init());
        services.AddSingleton<ISearchDictionary>(provider => provider.GetRequiredService<PhilKellyDictionaryService>());
        // verse key -> the dictionary entries quoting it (the reverse of the
        // entries' verse citations): built once from the quoting dictionaries
        services.AddSingleton(provider => new VerseQuotationIndex(provider.GetServices<ISearchDictionary>()));
        // eager: the lemma table is also the analyzer's, best loaded before indexing
        services.AddSingleton(LemmaTable.Instance);
        // eager for the same reason: the resolution layers narrow the lemma field
        services.AddSingleton(LemmaResolver.Instance);
        services.AddSingleton<DictionaryLookupService>();
        services.AddSingleton<DictionaryHistoryService>();
        services.AddSingleton<DictionaryAttestationService>();
        // filled from the index's term list in Configure, once the corpus is loaded
        services.AddSingleton<CorpusVocabulary>();
        services.AddSingleton<DictionaryBrowseService>();
        services.AddSingleton<LemmaIndexService>();
        services.AddSingleton<DictionaryStatsService>();
        services.AddSingleton<WorkService>();
        services.AddSingleton<DocumentSearchService>();
        services.AddSingleton<NewspaperSourceEnricher>();
        services.AddSingleton<OverviewSearchService2>();
        // TODO: Move config here
        services.AddSingleton<RecentDocumentsService>();
        services.AddSingleton<ContributionsService>();

        // In production, the React files will be served from this directory
        services.AddSpaStaticFiles(configuration =>
        {
            configuration.RootPath = "ClientApp/build";
        });
    }

    public void Configure(IApplicationBuilder app,
        IWebHostEnvironment env,
        WorkService workService,
        ILogger<Startup> logger,
        Searcher searcher,
        RecentDocumentsService recentDocumentsService,
        ContributionsService contributionsService,
        CorpusVocabulary vocabulary)
    {
        log = logger;
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();

            // Fail before the slow corpus load if a stale server from another
            // branch/worktree holds our ports - it would answer on our URLs and
            // the wrong code would get tested.
            DevPortGuard.EnsureListenPortsFree(Configuration["urls"] ?? "https://localhost:5001;http://localhost:5000");
            ViteDevServer.EnsureFreeOrOurs(ViteDevServerUrl);
        }
        else
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        var lConfig = Configuration.GetSection("Loading").Get<LoadConfig>();

        var databaseCount = SetupDatabase(workService, searcher, lConfig);
        var termFrequency = searcher.QueryTermFrequency();
        StatisticsController.Init(databaseCount, termFrequency, log);
        // the same list the statistics page counts: what the corpus actually says,
        // which is what tells a used word from a proposed one
        vocabulary.Init(termFrequency);
        SetupDictionaries();
        ScanPhrasesInBackground(vocabulary, workService, searcher, app.ApplicationServices);

        try
        {
            // the deployment generates newdocs.txt; a dev checkout has none, so
            // 'recently added' shows a random sample to develop against
            var latestDocuments = env.IsDevelopment()
                ? OpenDataLoader.RandomRecentDocuments(workService).Result
                : OpenDataLoader.LoadRecentDocuments(workService).Result;
            recentDocumentsService.Init(latestDocuments, log);
        }
        catch (Exception e)
        {
            log.LogError(e , "failed to read latest documents");
        }

        try
        {
            // warm the cache: the first computation reads every document's license file
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var contributors = contributionsService.GetContributors().Result;
            log.LogInformation("Found {Count} contributors in {Milliseconds}ms", contributors.Count, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception e)
        {
            log.LogError(e, "failed to compute contributions");
        }
            
        // app.UseHttpsRedirection();
        
        // if index.html is stale, it points to files in /assets/ which no longer exist.
        var noCacheHtml = new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                var path = ctx.Context.Request.Path.Value ?? "";
                if (path is "/index.html" or "/manifest.json")
                {
                    ctx.Context.Response.Headers.CacheControl = "no-cache";
                }
            }
        };

        app.UseStaticFiles();
        app.UseSpaStaticFiles(noCacheHtml);

        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllerRoute(
                name: "default",
                pattern: "{controller}/{action=Index}/{id?}");
        });

        if (!env.IsDevelopment())
        {
            // URLs which are neither a server route nor a SPA page must 404 rather
            // than serve the shell with a 200 (see SpaRouteGuard). Development is
            // excluded: there UseSpa also proxies Vite's own requests.
            app.UseSpaRouteGuard(workService);
        }

        app.UseSpa(spa =>
        {
            spa.Options.SourcePath = "ClientApp";
            spa.Options.DefaultPageStaticFileOptions = noCacheHtml;

            if (env.IsDevelopment())
            {
                // Start and wait for the Vite dev server and proxy to it, so `dotnet run` is one command.
                spa.UseProxyToSpaDevelopmentServer(() => ViteDevServer.EnsureRunningAsync(ViteDevServerUrl));
            }
        });

        GC.Collect();
    }

    internal static void SetupDictionaries()
    {
        // This saves ~700MB RAM compared to using F# for XML reading... sorry
        // files sourced from Phil Kelly https://www.learnmanx.com/page_342285.html
        using (FileStream manx = File.OpenRead(GetLocalFile("Resources", "manx.json")))
        {
            ManxToEnglishDictionary = ToCaseInsensitiveDict(manx);
        }
        using (FileStream english = File.OpenRead(GetLocalFile("Resources", "english.json")))
        {
            EnglishToManxDictionary = ToCaseInsensitiveDict(english);
        }

        return;

        static Dictionary<string, IList<string>> ToCaseInsensitiveDict(FileStream fileStream)
        {
            var dict = DeserializeAsync<Dictionary<string, IList<string>>>(fileStream).Result
                ?? throw new InvalidOperationException($"dictionary '{fileStream.Name}' deserialized to null");
            return new Dictionary<string, IList<string>>(dict, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Reads the corpus for the books' phrase headwords, behind the running server.
    ///
    /// Not awaited, and deliberately: it walks every line of the corpus, and the
    /// answer it works out is only what a browse index greys by. Nobody should wait
    /// on the door for it — and until it lands the vocabulary says so rather than
    /// guessing, so a page can offer the reader the corpus search instead.
    /// </summary>
    /// <summary>How often the phrase pass says it is still going. It takes some
    /// thirteen seconds while the server is already answering pages, so without a
    /// heartbeat there is nothing at all to see between "started" and "done" —
    /// and a phrase page saying "still reading the corpus" would have no log to
    /// be read against.</summary>
    private static readonly TimeSpan ScanHeartbeat = TimeSpan.FromSeconds(5);

    private void ScanPhrasesInBackground(CorpusVocabulary vocabulary, WorkService workService,
        Searcher searcher, IServiceProvider services)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                // the recordings first: a couple of dozen documents against the
                // corpus's hundreds, and the front page's audio numbers wait on
                // nothing else
                var stats = services.GetRequiredService<DictionaryStatsService>();
                var recordings = (await workService.GetAll())
                    .Where(x => x.Name.StartsWith("🎥")).ToList();
                stats.InitAudio(recordings.Count, recordings
                    .SelectMany(x => searcher.AllLines(x.Ident)
                        .Where(l => l.IsManxLanguage)
                        .Select(l => l.NormalizedStatsManx)));
                var headwords = services.GetServices<ISearchDictionary>()
                    .SelectMany(x => x.Headwords)
                    // the lemma tables' forms ride along: the lemma tree greys a
                    // multiword form ('er n'aase') by the same read of the corpus
                    .Concat(LemmaTable.Instance.AllForms)
                    // and the particle phrases ('e gheiney'), which are vias
                    // rather than forms: the tree counts a particle row by its
                    // phrase, not by the bare spelling
                    .Concat(LemmaTable.Instance.ParticlePhrases)
                    .ToList();
                var scan = Task.Run(() =>
                    vocabulary.ScanPhrases(headwords, CorpusLines(workService, searcher)));
                while (await Task.WhenAny(scan, Task.Delay(ScanHeartbeat)) != scan)
                {
                    log.LogInformation(
                        "Still reading the corpus for {Count} headwords ({Seconds:F0}s so far)",
                        headwords.Count, stopwatch.Elapsed.TotalSeconds);
                }
                // faulted or not: WhenAny above swallows nothing this does not rethrow
                await scan;
                log.LogInformation("Scanned the corpus for {Count} headwords in {Milliseconds}ms",
                    headwords.Count, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception e)
            {
                // the vocabulary goes on saying it does not know, which is where it
                // started: every page is still readable, and none of them lies
                log.LogError(e, "failed to scan the corpus for phrase headwords");
            }
        });
    }

    /// <summary>The Manx of every line of the corpus, as the statistics count it: a
    /// document at a time, so only one document's lines are ever held</summary>
    private static IEnumerable<string> CorpusLines(WorkService workService, Searcher searcher)
    {
        foreach (var document in workService.GetAll().Result)
        {
            foreach (var line in searcher.AllLines(document.Ident))
            {
                if (line.IsManxLanguage)
                {
                    yield return line.NormalizedStatsManx;
                }
            }
        }
    }

    internal (long totalDocuments, long totalManxTerms) SetupDatabase(WorkService workService, Searcher searcher, LoadConfig? lConfig)
    {
        // load all the document manifests first, so the (parallel) indexing is one batch
        var allDocuments = new List<Document>();

        bool ignoreClosedData = lConfig?.OpenDataOnly ?? false;
        if (!ignoreClosedData) try
            {
                List<Document> closedSourceDocument = ClosedDataLoader.LoadDocumentsFromFile().Cast<Document>().ToList();
                log.LogInformation("Loaded {Count} documents", closedSourceDocument.Count);
                allDocuments.AddRange(closedSourceDocument);
            }
            catch (Exception e)
            {
                log.LogError(e, "Failed loading documents");
            }
        // Try adding open source documents
        try
        {
            List<Document> ossDocuments = OpenDataLoader.LoadDocumentsFromFile(lConfig).Cast<Document>().ToList();
            log.LogInformation("Loaded {OssDocumentsCount} documents", ossDocuments.Count);
            allDocuments.AddRange(ossDocuments);
        }
        catch (Exception e)
        {
            log.LogError(e, "Failed loading documents");
        }

        allDocuments = WithoutDuplicates(allDocuments, log);

        AddDocuments(allDocuments, workService, searcher);
        var totalDocuments = (long)allDocuments.Count;

        var stopWatch = System.Diagnostics.Stopwatch.StartNew();
        searcher.OnAllDocumentsAdded();
        log.LogDebug("compacted in {CompactedInMilliseconds}", stopWatch.ElapsedMilliseconds);

        var totalTerms = searcher.CountManxTerms();
        return (totalDocuments, totalTerms);
    }

    /// <summary>
    /// Drops documents whose ident was already seen, e.g. an accidentally duplicated
    /// folder under OpenData: each copy would otherwise be indexed, duplicating every
    /// line of the document in search results. The data repo's lint should catch this;
    /// the server merely warns and carries on with the first copy.
    /// </summary>
    internal static List<Document> WithoutDuplicates(List<Document> documents, ILogger? log)
    {
        // not firstByIdent.Values: preserve order, and allow nulls
        var ret = new List<Document>();
        var firstByIdent = new Dictionary<string, Document>();
        foreach (var document in documents)
        {
            if (document.Ident == null || firstByIdent.TryAdd(document.Ident, document))
            {
                ret.Add(document);
                continue;
            }
            log?.LogWarning(
                "Ignoring duplicate document '{Ident}' from '{Path}': already loaded from '{OriginalPath}'",
                document.Ident, document.RelativeCsvPath, firstByIdent[document.Ident].RelativeCsvPath);
        }
        return ret;
    }

    private void AddDocuments(List<Document> documents, WorkService workService, Searcher searcher)
    {
        // Parallel: reading + normalizing + tokenizing the documents is CPU-bound and
        // IndexWriter.AddDocument is designed for concurrent use (#303).
        // Large documents are indexed in chunks so one document can't hold back the batch:
        // line order in the index doesn't matter, every consumer orders by CsvLineNumber.
        // Note: a document which fails to load no longer aborts the rest of its batch.
        const int linesPerChunk = 500;
        Parallel.ForEach(documents, document =>
        {
            try
            {
                List<DocumentLine> data = document.LoadPreparedLines();
                Parallel.ForEach(data.Chunk(linesPerChunk), chunk => searcher.AddDocument(document, chunk));
                workService.AddWork(document);
            }
            catch (Exception e)
            {
                log.LogError(e, "Failed loading document {Ident}", document.Ident);
            }
        });
    }

    public static string GetLocalFile(params string[] inputPath)
    {
        String[] path = new string[inputPath.Length + 1];
        path[0] = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < inputPath.Length; i++)
        {
            path[i + 1] = inputPath[i];
        }

        return Path.Combine(path);
    }

}