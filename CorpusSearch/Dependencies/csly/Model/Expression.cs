namespace CorpusSearch.Dependencies.csly.Model;

public abstract class Expression(string v)
{
    public string Term => v;

    public override string ToString()
    {
        return v;
    }
}