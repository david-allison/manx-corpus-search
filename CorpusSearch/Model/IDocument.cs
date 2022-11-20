using System;
using System.Collections.Generic;
using System.Linq;
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

        public string GitHubRepo { get; }
        public string RelativeCsvPath { get; }
        string Notes { get; }
        string Source { get; }

        object GetExtensionData(string key);
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
    }
}
