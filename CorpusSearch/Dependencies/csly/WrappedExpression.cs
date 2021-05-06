namespace Codex_API.Dependencies.csly
{
    internal class WrappedExpression : Expression
    {
        private string v1;
        public Expression Wrapped { get;  }
        private string v2;

        public WrappedExpression(string v1, Expression middle, string v2) : base("wrapped")
        {
            this.v1 = v1;
            this.Wrapped = middle;
            this.v2 = v2;
        }

        

        public override string ToString()
        {
            return $"{v1} {Wrapped} {v2}";
        }
    }
}