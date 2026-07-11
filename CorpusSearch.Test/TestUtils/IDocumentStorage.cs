namespace CorpusSearch.Test.TestUtils;

public interface IDocumentStorage
{
    void AddDocument(string name, params Line[] data);
}

public class Line
{
    public required string English { get; set; }
    public required string Manx { get; set; }
}