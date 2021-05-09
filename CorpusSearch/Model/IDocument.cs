using System;

namespace Codex_API.Model
{
    public interface IDocument
    {
        string Name { get; }
        /// <summary>Unique Identifier for the document</summary>
        string Ident { get; }

        public DateTime? CreatedCircaStart { get; }
        public DateTime? CreatedCircaEnd { get; }

        /// <summary>(optional) link to PDF</summary>
        public string ExternalPdfLink { get; }
    }
}
