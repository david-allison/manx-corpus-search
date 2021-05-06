using System.Collections.Generic;

namespace Codex_API.Dependencies.csly
{
    internal class AdjacentExpression : Expression
    {
        private IEnumerable<Expression> enumerable;

        public AdjacentExpression(IEnumerable<Expression> enumerable) : base("")
        {
            this.enumerable = enumerable;
        }
    }
}