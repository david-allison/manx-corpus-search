using System;
using System.Collections.Generic;
using CorpusSearch.Dependencies.CsvHelper;
using Newtonsoft.Json;

namespace CorpusSearch.Model
{
    public abstract class Document : IDocument
    {
        public string Name { get; set; }
        public string Ident { get; set; }
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

        public string CsvFileName { get; set; }

        /// <summary>Optional HTTP link to a PDF file (not a relative path)</summary>
        /// <remarks>Ensure that #page=n works on PC for a link like this</remarks>
        /// <remarks>I'm currenlty hosting these on Google Drive: I don't expect this to be a problem given small search volumes, but we may need a more permanent form of storage</remarks>
        public string ExternalPdfLink { get; set; }
        public DateTime? CreatedCircaStart { get; set; }
        public DateTime? CreatedCircaEnd { get; set; }
        public abstract string GitHubRepo { get; set; }
        public abstract string RelativeCsvPath { get; }

        public string Original { get; set; }
        public string Notes { get; set; }

        public string Source { get; set; }

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
    }
    
}
