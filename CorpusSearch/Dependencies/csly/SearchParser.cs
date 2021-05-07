using sly.lexer;
using sly.parser;
using sly.parser.generator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Codex_API.Dependencies.csly
{
    public enum ExpressionToken
    {
        [Lexeme("[0-9]+")]
        INT = 1,
        [Lexeme("\\s", IsSkippable = true)] // TODO: WS TODO: also ',', '?', '*', '(', ')', ':', '^', '"'
        WS = 2,
        [Lexeme("\\(")]
        LPAREN = 6,
        [Lexeme("\\)")]
        RPAREN = 7,
        [Lexeme("and")]
        AND = 8,
        [Lexeme("or")]
        OR = 9, 
        [Lexeme("not")]
        NOT = 10,
        [Lexeme("[^\\s\\(\\)]+")] // not whitespace or brackets
        TOKEN = 11,
    }

    public class ExpressionParser
    {
        [Production("expression: INT")]
        public Expression intExpr(Token<ExpressionToken> intToken)
        {
            return new StringExpression("expression");
        }

        [Production("expression : subexpression")]
        public Expression ToExpression(Expression left)
        {
            return left;
        }

        [Production("expression : subexpression OR expression")]
        public Expression OrExpression(Expression left, Token<ExpressionToken> operatorToken, Expression right)
        {
            return new OrExpression(left, right);
        }

        [Production("expression : subexpression AND expression")]
        public Expression AndExpression(Expression left, Token<ExpressionToken> operatorToken, Expression right)
        {
            return new AndExpression(left, right);
        }

        [Production("expression : subexpression NOT expression")]
        public Expression NotExpression(Expression left, Token<ExpressionToken> operatorToken, Expression right)
        {
            return new NotExpression(left, right);
        }

        [Production("subexpression : LPAREN expression RPAREN")]
        public Expression ParenExpressionTerm(Token<ExpressionToken> lParen, Expression middle, Token<ExpressionToken> RParen)
        {
            return new WrappedExpression("(", middle, ")");
        }

        [Production("subexpression : LPAREN subexpression RPAREN")]
        public Expression ParenSubexpressionTerm(Token<ExpressionToken> lParen, Expression middle, Token<ExpressionToken> RParen)
        {
            return new WrappedExpression("(", middle, ")");
        }

        [Production("subexpression : INT")]
        public Expression Expression(Token<ExpressionToken> intToken)
        {
            return new StringExpression("term");
        }

        [Production("subexpression : TOKEN")]
        public Expression TokenExpression(Token<ExpressionToken> token)
        {
            return new StringExpression(token.StringWithoutQuotes);
        }

        [Production("subexpression : TOKEN TOKEN+")]
        public Expression TokenExpressionMulti(Token<ExpressionToken> head, List<Token<ExpressionToken>> token)
        {
            return new AdjacentWordExpression(new[] { head }.Concat(token).Select(x => x.StringWithoutQuotes));
        }

    }

    public class SearchParser
    {
        private Parser<ExpressionToken, Expression> parser;

        public SearchParser(Parser<ExpressionToken, Expression> parser)
        {
            this.parser = parser;
        }

        public static SearchParser GetParser()
        {
            var parserInstance = new ExpressionParser();
            var builder = new ParserBuilder<ExpressionToken, Expression>();
            var parser = builder.BuildParser(parserInstance, ParserType.EBNF_LL_RECURSIVE_DESCENT, "expression").Result;

            if (parser == null)
            {
                throw new Exception("no parser generated");
            }

            return new SearchParser(parser);
        }

        internal ParseResult<ExpressionToken, Expression> Parse(string expression)
        {
            var r = parser.Parse(expression);
            
            if (!r.IsError && r.Result != null)
            {
                Console.WriteLine($"result of <{expression}>  is {r.Result}");
                // outputs : result of <42 + 42>  is 84"
            }
            else
            {
                if (r.Errors != null && r.Errors.Any())
                {
                    // display errors
                    r.Errors.ForEach(error => Console.WriteLine(error.ErrorMessage));
                }
            }

            return r;
        }
    }
}
