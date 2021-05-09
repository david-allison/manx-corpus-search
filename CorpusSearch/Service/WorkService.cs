using Codex_API.Model;
using Dapper;
using System;
using System.Threading.Tasks;

namespace Codex_API.Service
{
    public class WorkService
    {
        /// <summary>
        /// The auto-incrementing ID of the documents
        /// </summary>
        /// <remarks>Might be better as SQL - might not as a constant ID is useful</remarks>
        private static int DocumentAddedCount { get; set; } = 0;

        /// <summary>Given an ident, get the document or throw</summary>
        public static async Task<IDocument> ByIdent(string ident)
        {
            return await Startup.conn.QuerySingleAsync<WorkServiceDocument>("SELECT name, ident, startDate as CreatedCircaStart, endDate as CreatedCircaEnd FROM works where ident = @ident", new { ident });
        }

        internal static void AddWork(IDocument document)
        {
            DocumentAddedCount++;
            int documentId = DocumentAddedCount;
            var workParams = new DynamicParameters();
            workParams.Add("id", documentId);
            workParams.Add("name", document.Name);
            workParams.Add("ident", document.Ident);
            workParams.Add("startdate", document.CreatedCircaStart);
            workParams.Add("enddate", document.CreatedCircaEnd);
            Startup.conn.Execute("INSERT INTO [works] (id, name, ident, startdate, enddate) VALUES (@id, @name, @ident, @startdate, @enddate)", workParams);
        }

        private class WorkServiceDocument : IDocument
        {
            public string Name { get; set; }
            public string Ident { get; set; }
            public DateTime? CreatedCircaStart { get; set; }
            public DateTime? CreatedCircaEnd { get; set; }
        }
    }
}
