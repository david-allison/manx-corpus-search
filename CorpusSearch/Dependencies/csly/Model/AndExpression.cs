namespace Codex_API.Dependencies.csly
{
    public class AndExpression : Expression
    {
        public Expression Left { get; }
        public Expression Right { get; }

        public AndExpression(Expression left, Expression right) : base("and")
        {
            Left = left;
            Right = right;
        }

        public override string ToString()
        {
            return $"{Left} {base.ToString()} {Right}";
        }
    }
}
