using System.Text.Json.Serialization;
using CorpusSearch.Services;

namespace CorpusSearch.Model
{
    public class DocumentLine
    {
        public string English { get; set; }
        public string Manx { get; set; }
        public int? Page { get; set; }
        public string Notes { get; set; }

        public string ManxOriginal { get; set; }
        public string EnglishOriginal { get; set; }
        
        public double? SubStart { get; set; }
        public double? SubEnd { get; set; }

        [JsonIgnore]
        // TODO: NBSP?
        public string NormalizedEnglish
        {
            get
            {
                string handled = NormalizeEnglish(English);
                return " " + handled + " ";
            }
        }

        [JsonIgnore]
        public string NormalizedManx
        {
            get
            {
                string handled = NormalizeManx(Manx);
                return " " + handled + " ";
            }
        }

        /// <summary>The Line Number in the CSV</summary>
        public int CsvLineNumber { get; set; }

        public static string NormalizeEnglish(string english, bool allowQuestionMark = false)
        {
            return english.RemovePunctuation(" ", allowQuestionMark).RemoveNewLines().NormalizeMicrosoftWordQuotes().RemoveBrackets().RemoveDoubleQuotes().ToLower();
        }

        public static string NormalizeManx(string manx, bool allowQuestionMark = true)
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
