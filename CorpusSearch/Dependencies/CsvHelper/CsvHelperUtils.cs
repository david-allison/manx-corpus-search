using Codex_API.Model;
using CsvHelper;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Codex_API.Dependencies.CsvHelper
{
    public class CsvHelperUtils
    {
        public static List<DocumentLine> LoadCsv(string path)
        {
            using (var reader = new StreamReader(path))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Context.RegisterClassMap<DocumentLineMap>();
                List<DocumentLine> results = csv.GetRecords<DocumentLine>().ToList();
                return results;
            }
        }
    }
}
