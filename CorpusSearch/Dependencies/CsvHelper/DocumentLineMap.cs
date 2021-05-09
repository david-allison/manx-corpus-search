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
        }
    }
}
