using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;

namespace CorpusSearch.Service;

public static class CsvUtils
{
    public static FileResult ExportCsv<T>(this Controller application, IEnumerable<T> input, string fileName)
    {
        var memoryStream = new MemoryStream();
        var streamWriter = new StreamWriter(memoryStream, Encoding.UTF8); // No 'using' around this as it closes the underlying stream. StreamWriter.Dispose() is only really important when you're dealing with actual files anyhow.

        using (var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture, true)) // Note the last argument being set to 'true'
            csvWriter.WriteRecords(input);

        streamWriter.Flush(); // Perhaps not necessary, but CsvWriter's documentation does not mention whether the underlying stream gets flushed or not

        memoryStream.Position = 0;

        application.Response.Headers["Content-Disposition"] = $"attachment; filename={fileName}";

        return application.File(memoryStream, "text/csv");
    }
}