#nullable disable // not yet migrated, see the .csproj
namespace CorpusSearch.Test.TestUtils;

public interface IDocumentStorage
{
    void AddDocument(string name, params Line[] data);
}

public class Line
{
    public string English { get; set; }
    public string Manx { get; set; }
}