namespace CorpusSearch.Dependencies.csly
{
    public class NotExpression(Expression left, Expression right) : Expression("not")
    {
        public Expression Left { get; } = left;
        public Expression Right { get; } = right;

        public override string ToString()
        {
            return $"{Left} [{base.ToString()} {Right}]";
        }
    }
}
