using CorpusSearch.Model;
using CsvHelper;
using System.Collections.Generic;

namespace CorpusSearch.Dependencies.CsvHelper;

public class CsvHelperUtils
{
    public static List<DocumentLine> LoadCsv(string path)
    {
        using var csv = CsvReader.FromFile(path, new CsvReaderOptions
        {
            Separator = ',',
            TrackLineNumbers = true,
            MalformedData = CsvMalformedDataHandling.Throw,
            MissingField = CsvMissingFieldHandling.Throw,
        });
        var results = new List<DocumentLine>();
        while (csv.Read())
        {
            var line = csv.GetRecord<DocumentLine>();
            line.CsvLineNumber = EndLineNumber(csv);
            results.Add(line);
        }
        return results;
    }

    private static int EndLineNumber(CsvReader reader)
    {
        // v33's RawRow was the last physical line of a record, including any
        // quoted newlines. Existing links and line ranges use those numbers.
        var lineNumber = reader.LineNumber;
        byte previous = 0;
        foreach (var value in reader.RawRecord)
        {
            if (value == '\r' || (value == '\n' && previous != '\r'))
            {
                lineNumber++;
            }
            previous = value;
        }
        return checked((int)lineNumber);
    }
}
