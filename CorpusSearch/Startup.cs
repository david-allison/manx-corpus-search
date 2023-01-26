using CorpusSearch.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SpaServices.ReactDevelopmentServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CorpusSearch.Model;
using CorpusSearch.Dependencies.csly;
using CorpusSearch.Dependencies;
using CorpusSearch.Service;
using CorpusSearch.Utils;
using static System.Text.Json.JsonSerializer;

namespace CorpusSearch
{
    public class Startup
    {
        public static Dictionary<string, IList<string>> EnglishToManxDictionary { get; set; }
        public static Dictionary<string, IList<string>> ManxToEnglishDictionary { get; set; }


        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }


        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddControllersWithViews();

            services.AddSingleton(provider => LuceneIndex.GetInstance());
            services.AddSingleton(provider => SearchParser.GetParser());
            services.AddSingleton<Searcher>();
            services.AddSingleton(provider => CregeenDictionaryService.Init());
            services.AddSingleton<ISearchDictionary>(provider => provider.GetService<CregeenDictionaryService>());
            services.AddSingleton<WorkService>();
            services.AddSingleton<DocumentSearchService>();
            services.AddSingleton<NewspaperSourceEnricher>();
            services.AddSingleton<OverviewSearchService2>();

            // In production, the React files will be served from this directory
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "ClientApp/build";
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, WorkService workService, Searcher searcher)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            if (!AnonymousAnalytics.Init())
            {
                Console.WriteLine("Failed to init anonymous analytics. Was CORPUS_SEARCH_SEGMENT_KEY set?");
            }
            

            SetupDatabase(workService, searcher);

            SetupDictionaries();



            // app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseSpaStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller}/{action=Index}/{id?}");
            });

            app.UseSpa(spa =>
            {
                spa.Options.SourcePath = "ClientApp";

                if (env.IsDevelopment())
                {
                    spa.UseReactDevelopmentServer(npmScript: "start");
                }
            });

            GC.Collect();
        }

        internal static void SetupDictionaries()
        {
            Dictionary<string, IList<string>> ToCaseInsensitiveDict(FileStream fileStream) 
            {
                var dict = DeserializeAsync<Dictionary<string, IList<string>>>(fileStream).Result;
                return new Dictionary<string, IList<string>>(dict, StringComparer.OrdinalIgnoreCase);
            }
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
        }

        internal static void SetupDatabase(WorkService workService, Searcher searcher)
        {
            try
            {
                List<Document> closedSourceDocument = ClosedDataLoader.LoadDocumentsFromFile().Cast<Document>().ToList();
                Console.WriteLine($"Loaded {closedSourceDocument.Count} documents");
                AddDocuments(closedSourceDocument, workService, searcher);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed loading documents: {e}");
            }


            // Try adding open source documents
            try
            {
                List<Document> ossDocuments = OpenDataLoader.LoadDocumentsFromFile().Cast<Document>().ToList();
                Console.WriteLine($"Loaded {ossDocuments.Count} documents");
                AddDocuments(ossDocuments, workService, searcher);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed loading documents: {e}");
            }

            var stopWatch = System.Diagnostics.Stopwatch.StartNew();
            searcher.OnAllDocumentsAdded();
            Console.WriteLine($"compacted in {stopWatch.ElapsedMilliseconds}");
        }

        private static void AddDocuments(List<Document> documents, WorkService workService, Searcher searcher)
        {
            foreach (var document in documents)
            {
                AddDocument(document, workService, searcher);
            }
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

        private static void AddDocument(Document document, WorkService workService, Searcher searcher)
        {
            List<DocumentLine> data = document.LoadLocalFile();

            searcher.AddDocument(document, data);

            workService.AddWork(document);
        }
    }
}
