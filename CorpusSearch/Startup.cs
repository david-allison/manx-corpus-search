using CorpusSearch.Services;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SpaServices.ReactDevelopmentServer;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CorpusSearch.Model;
using CorpusSearch.Dependencies.CsvHelper;
using CorpusSearch.Dependencies.csly;
using CorpusSearch.Dependencies;
using CorpusSearch.Service;

namespace CorpusSearch
{
    public partial class Startup
    {
        public static Searcher searcher;

        public static Dictionary<string, IList<string>> EnglishDictionary { get; set; }
        public static Dictionary<string, IList<string>> ManxDictionary { get; set; }

        public static SqliteConnection conn { get; set; }


        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }


        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddControllersWithViews();

            // In production, the React files will be served from this directory
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "ClientApp/build";
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
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

            var luceneIndex = LuceneIndex.GetInstance();
            var parser = SearchParser.GetParser();
            Startup.searcher = new Searcher(luceneIndex, parser);

            SetupDatabase();

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
            // This saves ~700MB RAM compared to using F# for XML reading... sorry
            // files sourced from https://www.learnmanx.com/page_342285.html - TODO; confirm copyright
            using (FileStream manx = File.OpenRead(GetLocalFile("Resources", "manx.json")))
            {
                ManxDictionary = System.Text.Json.JsonSerializer.DeserializeAsync<Dictionary<string, IList<string>>>(manx).Result;
            }
            using (FileStream english = File.OpenRead(GetLocalFile("Resources", "english.json")))
            {
                EnglishDictionary = System.Text.Json.JsonSerializer.DeserializeAsync<Dictionary<string, IList<string>>>(english).Result;
            }
        }

        internal static void SetupDatabase()
        {
            SetupSqlite();


            try
            {
                List<Document> closedSourceDocument = ClosedDataLoader.LoadDocumentsFromFile().Cast<Document>().ToList();
                Console.WriteLine($"Loaded {closedSourceDocument.Count} documents");
                AddDocuments(closedSourceDocument);
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
                AddDocuments(ossDocuments);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed loading documents: {e}");
            }
        }

        private static void AddDocuments(List<Document> documents)
        {
            foreach (var document in documents)
            {
                AddDocument(document);
            }
        }

        private static string GetLocalFile(params string[] inputPath)
        {
            String[] path = new string[inputPath.Length + 1];
            path[0] = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < inputPath.Length; i++)
            {
                path[i + 1] = inputPath[i];
            }

            return Path.Combine(path);
        }

        public class Document : IDocument
        {
            public string Name { get; set; }
            public string Ident { get; set; }
            /// <summary>
            /// The time that the manx translation was created.
            /// </summary>
            public DateTime? Created {set
                {
                    CreatedCircaEnd = value;
                    CreatedCircaStart = value;
                }
            }

            public string CsvFileName { get; set; }

            /// <summary>Optional HTTP link to a PDF file (not a relative path)</summary>
            /// <remarks>Ensure that #page=n works on PC for a link like this</remarks>
            /// <remarks>I'm currenlty hosting these on Google Drive: I don't expect this to be a problem given small search volumes, but we may need a more permanent form of storage</remarks>
            public string ExternalPdfLink { get; set; }
            public DateTime? CreatedCircaStart { get; set; }
            public DateTime? CreatedCircaEnd { get; set; }

            internal virtual List<DocumentLine> LoadLocalFile()
            {
                return CsvHelperUtils.LoadCsv(GetLocalFile("Resources", CsvFileName));
            }
        }

        private static void AddDocument(Document document)
        {
            List<DocumentLine> data = document.LoadLocalFile();

            searcher.AddDocument(document, data);



            WorkService.AddWork(document);
        }

        private static void SetupSqlite()
        {
            var connectionString = new SqliteConnectionStringBuilder()
            {
                Mode = SqliteOpenMode.Memory
            };
            SQLitePCL.Batteries.Init();

            conn = new SqliteConnection(connectionString.ToString());
            conn.Open();
            conn.ExecSql("create table works (" +
                "id int PRIMARY KEY, " +
                "name varchar, " +
                "ident varchar, " +
                "startdate datetime DEFAULT NULL, " +
                "enddate datetime DEFAULT NULL," +
                "pdfLink varchar DEFAULT NULL)");
        }
    }
}
