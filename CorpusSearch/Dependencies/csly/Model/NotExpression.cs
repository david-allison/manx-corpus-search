namespace Codex_API.Dependencies.csly
{
    public class NotExpression : Expression
    {
        private Expression left;

        public NotExpression(Expression left) : base("not")
        {
            this.left = left;
        }

        public override string ToString()
        {
            return $"[{base.ToString()} {left}]";
        }
    }
}
