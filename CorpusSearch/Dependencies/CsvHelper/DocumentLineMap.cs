using System;
using CorpusSearch.Model;
using CsvHelper.Configuration;

namespace CorpusSearch.Dependencies.CsvHelper
{
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
            Map(m => m.CsvLineNumber).Convert(row => row.Parser.RawRow);
            Map(m => m.ManxOriginal).Convert(row =>
            {
                if (!row.TryGetField<string>("Manx Original", out var field))
                {
                    return null;
                }
                return string.IsNullOrWhiteSpace(field) ? null : field;

            });
            Map(m => m.EnglishOriginal).Convert(row =>
            {
                if (!row.TryGetField<string>("English Original", out var field))
                {
                    return null;
                }
                return string.IsNullOrWhiteSpace(field) ? null : field;
            });
        }
    }
}
