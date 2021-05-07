using Codex_API.Model;
using CsvHelper.Configuration;

namespace Codex_API.Dependencies.CsvHelper
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
