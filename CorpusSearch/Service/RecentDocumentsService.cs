using System;
using System.Collections.Generic;
using System.Linq;
using CorpusSearch.Model;

namespace CorpusSearch.Service;

public class RecentDocumentsService
{
    private List<RecentDocument> documents = [];
    public void Init(List<RecentDocument> latestDocuments)
    {
        Console.WriteLine("Found {0} latest documents", latestDocuments.Count);
        documents = latestDocuments;
    }

    public List<LatestDocumentDto> GetLatestDocuments() =>
        documents.Select(x => new LatestDocumentDto(x.Document.Name, x.Document.Ident, x.ModificationTime)).ToList();

    public record LatestDocumentDto(string Name, string Ident, DateTime Uploaded);
}
