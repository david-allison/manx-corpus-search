using System.Text.Json.Serialization;

namespace CorpusSearch.Model;

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
        
    /// <summary>The name of the speaker in a transcription. Nullable</summary>
    public string Speaker { get; set; }
        
    public long? MatchesInLine { get; set; }

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

    // Trim: punctuation is replaced with spaces, so "jee." would otherwise become "jee " and never
    // match an indexed term (#237)
    public static string NormalizeEnglish(string english, bool allowQuestionMark = false)
    {
        return NormalizationMapper.NormalizeEnglishMapped(english, allowQuestionMark).Text;
    }

    public static string NormalizeManx(string manx, bool allowQuestionMark = true)
    {
        return NormalizationMapper.NormalizeManxMapped(manx, allowQuestionMark).Text;
    }

}