using Codex_API.Model;

namespace Codex_API.Model
{
    /// <summary>A "search" on the front page: provides a summary of results, </summary>
    public delegate ScanResult Scan(string query, ScanOptions options);

    /// <summary>A result from a 'Scan' Query </summary>
    public class ScanResult
    {
        /// <summary>The total number of matches</summary>
        /// <example>two files: [["hello"], ["hello", "hello hello"]] would return 4 matches</example>
        public int NumberOfMatches { get; set; }
        /// <summary>A segment is each "translation" that a file contains</summary>
        /// <example>two files: [["hello"], ["hello", "hello hello"]] would return 3 segments</example>
        public int NumberOfSegments { get; internal set; }

        /// <summary>A document is the concept of a document in the corpus search</summary>
        /// <example>two files: [["hello"], ["hello", "hello hello"]] would return 2 documents</example>
        public int NumberOfDocuments { get; internal set; }
    }

}
