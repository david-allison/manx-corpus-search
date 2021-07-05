using CorpusSearch.Model;
using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.Threading.Tasks;

namespace CorpusSearch.Service
{
    public class WorkService
    {
        private readonly SqliteConnection conn;

        public WorkService(SqliteConnection connection)
        {
            this.conn = connection;
        }

        /// <summary>
        /// The auto-incrementing ID of the documents
        /// </summary>
        /// <remarks>Might be better as SQL - might not as a constant ID is useful</remarks>
        private static int DocumentAddedCount { get; set; } = 0;

        /// <summary>Given an ident, get the document or throw</summary>
        public async Task<IDocument> ByIdent(string ident)
        {
            return await conn.QuerySingleAsync<WorkServiceDocument>("SELECT " +
                "name, " +
                "ident, " +
                "startDate as CreatedCircaStart, " +
                "endDate as CreatedCircaEnd, " +
                "pdfLink as ExternalPdfLink, " +
                "github as GitHubRepo, " +
                "path as RelativeCsvPath, " +
                "notes as Notes " +
                "FROM works where ident = @ident", new { ident });
        }

        internal void AddWork(IDocument document)
        {
            DocumentAddedCount++;
            int documentId = DocumentAddedCount;
            var workParams = new DynamicParameters();
            workParams.Add("id", documentId);
            workParams.Add("name", document.Name);
            workParams.Add("ident", document.Ident);
            workParams.Add("startdate", document.CreatedCircaStart);
            workParams.Add("enddate", document.CreatedCircaEnd);
            workParams.Add("pdfLink", document.ExternalPdfLink);
            workParams.Add("github", document.GitHubRepo);
            workParams.Add("path", document.RelativeCsvPath);
            workParams.Add("notes", document.Notes);
            conn.Execute("INSERT INTO [works] (id, name, ident, startdate, enddate, pdfLink, github, path, notes) VALUES (@id, @name, @ident, @startdate, @enddate, @pdfLink, @github, @path, @notes)", workParams);
        }

        private class WorkServiceDocument : IDocument
        {
            public string Name { get; set; }
            public string Ident { get; set; }
            public DateTime? CreatedCircaStart { get; set; }
            public DateTime? CreatedCircaEnd { get; set; }
            public string ExternalPdfLink { get; set; }
            public string GitHubRepo { get; set; }
            public string RelativeCsvPath { get; set; }
            public string Notes { get; set; }
        }
    }
}
