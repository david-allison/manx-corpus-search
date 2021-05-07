using Codex_API.Services;

namespace Codex_API.Model
{
    public class DocumentLine
    {
        public string English { get; set; }
        public string Manx { get; set; }
        public int? Page { get; set; }
        public string Notes { get; set; }

        // TODO: NBSP?
        public string NormalizedEnglish
        {
            get
            {
                string handled = English.RemovePunctuation(" ").RemoveNewLines().NormalizeMicrosoftWordQuotes().RemoveBrackets().RemoveDoubleQuotes();
                return " " + handled + " ";
            }
        }

        public string NormalizedManx
        {
            get
            {
                string handled = NormalizeManx(Manx);
                return " " + handled + " ";
            }
        }

        public static string NormalizeManx(string manx)
        {
            string handled = manx.RemovePunctuation(" ")
                .RemoveNewLines()
                .NormalizeMicrosoftWordQuotes()
                .RemoveBrackets()
                .RemoveColon() //example: "gra:"
                .RemoveDoubleQuotes();
            return handled;
        }

    }

}
