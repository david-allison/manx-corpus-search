using CorpusSearch.Services;

namespace CorpusSearch.Model
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
                string handled = NormalizeEnglish(English);
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

        public static string NormalizeEnglish(string english, bool allowQuestionMark = false)
        {
            return english.RemovePunctuation(" ", allowQuestionMark).RemoveNewLines().NormalizeMicrosoftWordQuotes().RemoveBrackets().RemoveDoubleQuotes().ToLower();
        }

        public static string NormalizeManx(string manx, bool allowQuestionMark = false)
        {
            string handled = manx.RemovePunctuation(" ", allowQuestionMark)
                .RemoveNewLines()
                .NormalizeMicrosoftWordQuotes()
                .RemoveBrackets()
                .RemoveColon() //example: "gra:"
                .RemoveDoubleQuotes()
                .ToLower();
            return handled;
        }

    }

}
