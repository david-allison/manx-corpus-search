namespace CorpusSearch.Dependencies.csly.Model;

public class OrExpression(Expression left, Expression right) : Expression("or")
{
    public Expression Left { get; } = left;
    public Expression Right { get; } = right;

    public override string ToString()
    {
        return $"[{Left} {base.ToString()} {Right}]";
    }
}