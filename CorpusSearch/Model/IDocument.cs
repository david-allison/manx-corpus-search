using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Web;

namespace CorpusSearch.Model
{
    public interface IDocument
    {
        string Name { get; }
        /// <summary>Unique Identifier for the document</summary>
        string Ident { get; }

        public DateTime? CreatedCircaStart { get; }
        public DateTime? CreatedCircaEnd { get; }

        /// <summary>(optional) link to PDF</summary>
        public string ExternalPdfLink { get; }
        
        /// <summary>(optional) link to Google Books</summary>
        /// <remarks>Google books uses "&amp;pg=p7" to link to pages, slightly different from a PDF</remarks>
        public string GoogleBooksId { get; }

        public string GitHubRepo { get; }
        public string RelativeCsvPath { get; }
        string Notes { get; }
        string Source { get; }
        
        string Original { get; }

        IDictionary<string, object> GetAllExtensionData();
    }

    public static class IDocumentExtensions
    {
        /// <summary>(nullable) - full link to GitHub</summary>
        public static string GetGitHubLink(this IDocument target)
        {
            // BUG: THis needs to handle a + in the CSV Path as it's a valid character
            if (String.IsNullOrEmpty(target.GitHubRepo) || string.IsNullOrEmpty(target.RelativeCsvPath))
            {
                return null;
            }
            var path = target.RelativeCsvPath;
            path = HttpUtility.UrlEncode(path).Replace("+", "%20").Replace("%5c", "/");
            return $"https://github.com/{target.GitHubRepo}/blob/master/{path}";
        }
        
        /// <summary>(nullable) - full link to GitHub</summary>
        public static string GetDownloadTextLink(this IDocument target)
        {
            return GetGitHubLink(target)?
                .Replace("https://github.com", "https://raw.githubusercontent.com")
                .Replace("/blob/", "/");
        }
        
        /// <summary>(nullable) - full link to GitHub</summary>
        public static string GetDownloadMetadataLink(this IDocument target)
        {
            return GetDownloadTextLink(target)?.Replace("document.csv", "manifest.json.txt");
        }

        private static DatePair GetDatePair(this IDocument document)
        {
            if (document.CreatedCircaStart == null && document.CreatedCircaEnd == null)
            {
                return null;
            }

            if (!document.CreatedCircaEnd.HasValue)
            {
                Debug.Assert(document.CreatedCircaStart != null);
                return new DatePair(document.CreatedCircaStart.Value, null);
            }

            if (!document.CreatedCircaStart.HasValue)
            {
                return new DatePair(document.CreatedCircaEnd.Value, null);
            }

            if (document.CreatedCircaStart!.Value.Year == document.CreatedCircaEnd!.Value.Year)
            {
                return new DatePair(document.CreatedCircaStart.Value, null);
            }

            return new DatePair(document.CreatedCircaStart.Value, document.CreatedCircaEnd.Value);
        }
        public static string GetFullYear(this IDocument document)
        {
            var datePair = document.GetDatePair();
            if (datePair == null) {
                return "";
            }

            if (!datePair.Second.HasValue)
            {
                return datePair.First.Year.ToString();
            }
            
            return datePair.First.Year + "-" + datePair.Second.Value.Year;
        }
        
        public static string GetFullDate(this IDocument document)
        {
            var datePair = document.GetDatePair();
            if (datePair == null) {
                return "";
            }

            if (!datePair.Second.HasValue)
            {
                return datePair.First.ToString("ddd, dd MMM yyy");
            }
            
            return datePair.First.ToString("ddd, dd MMM yyy") + "-" + datePair.Second.Value.ToString("ddd, dd MMM yyy");
        }

        record DatePair(DateTime First, DateTime? Second);

        public static object GetExtensionData(this IDocument document, string key)
        {
            return document.GetAllExtensionData()[key];
        }
    }
}
