namespace CorpusSearch.Dependencies.csly
{
    public class NotExpression : Expression
    {
        public Expression Left { get; }
        public Expression Right { get; }

        public NotExpression(Expression left, Expression right) : base("not")
        {
            this.Left = left;
            this.Right = right;
        }

        public override string ToString()
        {
            return $"{Left} [{base.ToString()} {Right}]";
        }
    }
}
