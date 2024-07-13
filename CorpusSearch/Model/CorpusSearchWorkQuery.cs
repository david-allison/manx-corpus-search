namespace CorpusSearch.Model;

public class CorpusSearchWorkQuery(string query)
{
    public string Query { get; } = query;

    public string Ident { get; set; }
    public bool Manx { get; set; }
    public bool English { get; set; }

    internal bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Query) || string.IsNullOrEmpty(Ident) || Query.Length > 100)
        {
            return false;
        }

        return Manx || English;
    }
}