namespace CorpusSearch.Dependencies.csly
{
    public abstract class Expression
    {
        private string v;

        public Expression(string v)
        {
            this.v = v;
        }

        public string Term => v;

        public override string ToString()
        {
            return v;
        }
    }
}
