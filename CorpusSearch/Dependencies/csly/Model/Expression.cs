namespace CorpusSearch.Dependencies.csly
{
    public abstract class Expression(string v)
    {
        public string Term => v;

        public override string ToString()
        {
            return v;
        }
    }
}
