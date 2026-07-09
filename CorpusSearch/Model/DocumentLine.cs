using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CorpusSearch.Model;

public class DocumentLine
{
    public string English { get; set; }
    public string Manx { get; set; }
    public int? Page { get; set; }
    public string Notes { get; set; }

    /// <summary>Ranges of <see cref="Manx"/> which matched the query. Null unless Manx was searched.</summary>
    public IReadOnlyList<HighlightRange> ManxHighlights { get; set; }

    /// <summary>Ranges of <see cref="English"/> which matched the query. Null unless English was searched.</summary>
    public IReadOnlyList<HighlightRange> EnglishHighlights { get; set; }

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

    /// <summary>Case-preserving <see cref="NormalizedEnglish"/>: the case-sensitive indexed field text</summary>
    [JsonIgnore]
    public string NormalizedEnglishCased => " " + NormalizeEnglish(English, preserveCase: true) + " ";

    /// <summary>Case-preserving <see cref="NormalizedManx"/>: the case-sensitive indexed field text</summary>
    [JsonIgnore]
    public string NormalizedManxCased => " " + NormalizeManx(Manx, preserveCase: true) + " ";

    /// <summary>The Line Number in the CSV</summary>
    public int CsvLineNumber { get; set; }

    // Trim: punctuation is replaced with spaces, so "jee." would otherwise become "jee " and never
    // match an indexed term (#237)
    public static string NormalizeEnglish(string english, bool allowQuestionMark = false, bool preserveCase = false)
    {
        return NormalizationMapper.NormalizeEnglishMapped(english, allowQuestionMark, preserveCase).Text;
    }

    public static string NormalizeManx(string manx, bool allowQuestionMark = true, bool preserveCase = false)
    {
        return NormalizationMapper.NormalizeManxMapped(manx, allowQuestionMark, preserveCase).Text;
    }

}