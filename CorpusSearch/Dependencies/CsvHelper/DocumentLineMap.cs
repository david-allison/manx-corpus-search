using CorpusSearch.Model;
using CsvHelper.Configuration;

namespace CorpusSearch.Dependencies.CsvHelper;

public class DocumentLineMap : ClassMap<DocumentLine>
{
    public DocumentLineMap()
    {
        Map(m => m.English);
        Map(m => m.Manx);
        Map(m => m.Page).Optional();
        Map(m => m.Notes).Optional();
        Map(m => m.SubStart).Optional();
        Map(m => m.SubEnd).Optional();
        Map(m => m.Speaker).Optional();
        Map(m => m.Reference).Optional();
        // named after the manifest field it overrides (manxColumnLanguage): a bare
        // "Language" header would not say which column it describes
        Map(m => m.Language).Name("ManxColumnLanguage").Optional();
        Map(m => m.CsvLineNumber).Convert(args => args.Row.Parser.RawRow);
        Map(m => m.ManxOriginal).Convert(args =>
        {
            if (!args.Row.TryGetField<string>("Manx Original", out var field))
            {
                return null;
            }
            return string.IsNullOrWhiteSpace(field) ? null : field;

        });
        Map(m => m.EnglishOriginal).Convert(args =>
        {
            if (!args.Row.TryGetField<string>("English Original", out var field))
            {
                return null;
            }
            return string.IsNullOrWhiteSpace(field) ? null : field;
        });
    }
}