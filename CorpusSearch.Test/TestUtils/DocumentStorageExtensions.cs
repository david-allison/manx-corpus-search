using System.Linq;

namespace Codex_API.Test.TestUtils
{
    public static class DocumentStorageExtensions
    {
        public static void AddManxDoc(this IDocumentStorage target, string document, params string[] manx)
        {
            target.AddDocument(document, manx.Select(x => new Line { Manx = x }).ToArray());
        }
    }
}
