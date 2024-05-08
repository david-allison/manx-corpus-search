using CorpusSearch.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CorpusSearch.Service;

public class WorkService
{
    private Dictionary<string, IDocument> documentByIdent = new();

    /// <summary>Given an ident, get the document or throw</summary>
    public Task<IDocument> ByIdent(string ident)
    {
        return Task.FromResult(documentByIdent[ident]);
    }

    internal void AddWork(IDocument document)
    {
        documentByIdent.Add(document.Ident, document);
    }

    internal Task<List<IDocument>> GetAll()
    {
        return Task.FromResult(documentByIdent.Values.ToList());
    }

    internal Task<List<string>> GetIdentsBetween(DateTime minDate, DateTime maxDate)
    {
        List<string> results = documentByIdent.Where(x =>
                (x.Value.CreatedCircaEnd == null || x.Value.CreatedCircaEnd >= minDate)
                &&
                (x.Value.CreatedCircaStart == null || x.Value.CreatedCircaStart <= maxDate)
            )
            .Select(x => x.Key)
            .ToList();
        return Task.FromResult(results);
    }

    public bool HasIdent(string documentId) => documentByIdent.ContainsKey(documentId);
}