namespace Codex_API.Dependencies.csly
{
    public class OrExpression : Expression
    {
        public Expression Left { get; }
        public Expression Right { get; }

        public OrExpression(Expression left, Expression right) : base("or")
        {
            this.Left = left;
            this.Right = right;
        }

        public override string ToString()
        {
            return $"[{Left} {base.ToString()} {Right}]";
        }
    }
}
