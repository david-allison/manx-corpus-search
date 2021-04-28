using Codex_API.Services;
using CsvHelper;
using CsvHelper.Configuration;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SpaServices.ReactDevelopmentServer;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Codex_API.Controllers;
using Codex_API.Service;

namespace Codex_API
{
    public class Startup
    {
        public static Dictionary<string, IList<string>> EnglishDictionary { get; set; }
        public static Dictionary<string, IList<string>> ManxDictionary { get; set; }

        public static SqliteConnection conn { get; set; }

        /// <summary>
        /// The auto-incrementing ID of the documents
        /// </summary>
        /// <remarks>Might be better as SQL - might not as a constant ID is useful</remarks>
        private static int DocumentAddedCount { get; set; } = 0;


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

        public class DocumentLine
        {
            public string English { get; set; }
            public string Manx { get; set; }
            public int? Page { get; set; }
            public string Notes { get; set; }

            public string NormalizedEnglish 
            { 
                get
                {
                    return " " + Regex.Replace(English, SearchController.PUNCTUATION_REGEX, " ") + " ";
                } 
            }

            public string NormalizedManx
            {
                get
                {
                    string noPunctuation = Regex.Replace(Manx, SearchController.PUNCTUATION_REGEX, " ");
                    string noDiacritics = DiacriticService.Replace(noPunctuation);
                    return " " + noDiacritics + " ";
                }
            }
        }

        public class DocumentLineMap : ClassMap<DocumentLine>
        {
            public DocumentLineMap()
            {
                Map(m => m.English);
                Map(m => m.Manx);
                Map(m => m.Page).Optional();
                Map(m => m.Notes).Optional();
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

        public class Document
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

            /// <summary>
            /// Optional PDF link
            /// </summary>
            /// <remarks>TDO: Currently unused - hardcoded link</remarks>
            /// <remarks>Needs URLEncoding. Maybe on the server-side?</remarks>
            public string PdfFileName { get; set; }
            public DateTime? CreatedCircaStart { get; set; }
            public DateTime? CreatedCircaEnd { get; set; }

            internal virtual List<DocumentLine> LoadLocalFile()
            {
                return LoadCsv(GetLocalFile("Resources", CsvFileName));
            }
        }

        private static void AddDocument(Document document)
        {
            List<DocumentLine> data = document.LoadLocalFile(); 

            DocumentAddedCount++;

            int documentId = DocumentAddedCount;

            var workParams = new DynamicParameters();
            workParams.Add("id", documentId);
            workParams.Add("name", document.Name);
            workParams.Add("ident", document.Ident);
            workParams.Add("startdate", document.CreatedCircaStart);
            workParams.Add("enddate", document.CreatedCircaEnd);
            conn.Execute("INSERT INTO [works] (id, name, ident, startdate, enddate) VALUES (@id, @name, @ident, @startdate, @enddate)", workParams);


            var parameters = data.Where(d => !string.IsNullOrWhiteSpace(d.English) || !string.IsNullOrWhiteSpace(d.Manx)).Select(u =>
            {
                var param = new DynamicParameters();
                param.Add("manx", u.Manx);
                param.Add("english", u.English);
                param.Add("manx2", u.NormalizedManx);
                param.Add("english2", u.NormalizedEnglish);
                param.Add("work", documentId);
                param.Add("page", u.Page);
                param.Add("notes", u.Notes);
                return param;
            }).ToList();

            conn.Execute("INSERT INTO [translations] (manx, english, page, work, normalizedManx, normalizedEnglish, notes) VALUES (@manx, @english, @page, @work, @manx2, @english2, @notes)", parameters);
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
            /*            conn.CreateFunction<string, string, bool>("REGEXP", (s1, s2) => System.Text.RegularExpressions.Regex.IsMatch(s2, s1), true);*/
            conn.ExecSql("create table works (id int PRIMARY KEY, name varchar, ident varchar, startdate datetime DEFAULT NULL, enddate datetime DEFAULT NULL)");
            conn.ExecSql("create table translations (" +
                "pk INTEGER PRIMARY KEY AUTOINCREMENT, " +
                "english varchar, " +
                "manx varchar, " +
                "work id, " +
                "page int NULLABLE, " +
                "normalizedManx varchar, " +
                "normalizedEnglish varchar, " +
                "notes varchar NULLABLE, " +
                "FOREIGN KEY(work) REFERENCES works(id)" +
                ")");

        }

        public static List<DocumentLine> LoadCsv(string path)
        {
            using (var reader = new StreamReader(path))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Context.RegisterClassMap<DocumentLineMap>();
                List<DocumentLine> results = csv.GetRecords<DocumentLine>().ToList();
                return results;
            }
        }
    }
}
