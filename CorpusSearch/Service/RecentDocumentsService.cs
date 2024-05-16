using System;
using System.Collections.Generic;
using System.Linq;
using CorpusSearch.Model;
using Microsoft.Extensions.Logging;
using Serilog;

namespace CorpusSearch.Service;

public class RecentDocumentsService
{
    private List<RecentDocument> documents = [];
    public void Init(List<RecentDocument> latestDocuments, ILogger<Startup> log)
    {
        log.LogInformation("Found {Count} latest documents", latestDocuments.Count);
        documents = latestDocuments;
    }

    public List<LatestDocumentDto> GetLatestDocuments() =>
        documents.Select(x => new LatestDocumentDto(x.Document.Name, x.Document.Ident, x.ModificationTime)).ToList();

    public record LatestDocumentDto(string Name, string Ident, DateTime Uploaded);
}
