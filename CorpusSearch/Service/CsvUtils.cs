using System.Collections.Generic;
using System.IO;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;

namespace CorpusSearch.Service;

public static class CsvUtils
{
    public static FileResult ExportCsv<T>(this Controller application, IEnumerable<T> input, string fileName)
    {
        var memoryStream = new MemoryStream();
        using (var csvWriter = CsvWriter.Create(memoryStream,
                   new CsvWriterOptions { WriteByteOrderMark = true }, leaveOpen: true))
        {
            csvWriter.WriteRecords(input);
        }

        memoryStream.Position = 0;

        application.Response.Headers["Content-Disposition"] = $"attachment; filename={fileName}";

        return application.File(memoryStream, "text/csv");
    }
}
