using System;
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
    }
}
