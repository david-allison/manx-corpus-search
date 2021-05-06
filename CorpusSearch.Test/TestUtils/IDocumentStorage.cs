namespace Codex_API.Test.TestUtils
{
    public interface IDocumentStorage
    {
        void AddDocument(string name, params Line[] data);
    }

    public class Line
    {
        public string English { get; set; }
        public string Manx { get; set; }
    }
}
