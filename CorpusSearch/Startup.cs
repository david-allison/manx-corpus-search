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

        services.AddSingleton(provider => LuceneIndex.GetInstance());
        services.AddSingleton(provider => SearchParser.GetParser());
        services.AddSingleton<Searcher>();
        services.AddSingleton(provider => CregeenDictionaryService.Init(provider.GetRequiredService<ILogger<CregeenDictionaryService>>()));
        services.AddSingleton<ISearchDictionary>(provider => provider.GetRequiredService<CregeenDictionaryService>());
        services.AddSingleton(provider => KellyManxToEnglishDictionaryService.Init(provider.GetRequiredService<ILogger<KellyManxToEnglishDictionaryService>>()));
        services.AddSingleton<ISearchDictionary>(provider => provider.GetRequiredService<KellyManxToEnglishDictionaryService>());
        services.AddSingleton<DictionaryLookupService>();
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
        RecentDocumentsService recentDocumentsService)
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
        SetupDictionaries();

        try
        {
            var latestDocuments = OpenDataLoader.LoadRecentDocuments(workService).Result;
            recentDocumentsService.Init(latestDocuments, log);
        }
        catch (Exception e)
        {
            log.LogError(e , "failed to read latest documents");
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
                List<DocumentLine> data = document.LoadLocalFile();
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