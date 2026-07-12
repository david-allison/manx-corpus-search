using System;
using System.Collections.Generic;
using CorpusSearch.Dependencies.CsvHelper;
using Newtonsoft.Json;

namespace CorpusSearch.Model;

public abstract class Document : IDocument
{
    public required string Name { get; set; }
    public required string Ident { get; set; }
    /// <summary>
    /// The time that the manx translation was created.
    /// </summary>
    public DateTime? Created 
    {
        set
        {
            CreatedCircaEnd = value;
            CreatedCircaStart = value;
        }
    }

    public string CsvFileName { get; set; } = "document.csv";

    /// <summary>Optional HTTP link to a PDF file (not a relative path)</summary>
    /// <remarks>Ensure that #page=n works on PC for a link like this</remarks>
    /// <remarks>I'm currenlty hosting these on Google Drive: I don't expect this to be a problem given small search volumes, but we may need a more permanent form of storage</remarks>
    public string? ExternalPdfLink { get; set; }

    public string? GoogleBooksId { get; set; }
    public DateTime? CreatedCircaStart { get; set; }
    public DateTime? CreatedCircaEnd { get; set; }
    public abstract string? GitHubRepo { get; set; }
    public abstract string? RelativeCsvPath { get; }

    public string? Original { get; set; }
    public string? Notes { get; set; }

    public string? Source { get; set; }

    /// <summary>
    /// The language of Manx-column cells without a line-level Language value:
    /// "gv" if absent. A knowingly mixed collection declares "mixed" and relies on
    /// the line-level column.
    /// </summary>
    public string? ManxColumnLanguage { get; set; }

    [JsonExtensionData]
    public IDictionary<string, object> ExtensionData { get; set; } = new Dictionary<string, object>();

    public IDictionary<string, object> GetAllExtensionData()
    {
        return ExtensionData;
    }

    internal virtual List<DocumentLine> LoadLocalFile()
    {
        return CsvHelperUtils.LoadCsv(Startup.GetLocalFile("Resources", CsvFileName));
    }

    /// <summary>The document's lines, with each line's effective language resolved
    /// (see <see cref="DocumentLinePreparer"/>)</summary>
    internal List<DocumentLine> LoadPreparedLines()
    {
        var lines = LoadLocalFile();
        DocumentLinePreparer.Prepare(this, lines);
        return lines;
    }
}