using CorpusSearch.Dependencies.CsvHelper;
using CorpusSearch.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static CorpusSearch.Startup;

namespace CorpusSearch.Services
{
    public class OpenDataLoader
    {
        /// <summary>
        /// We copy files in from the "OpenData" directory, which is cloned into by git.
        /// </summary>
        /// <returns></returns>
        public static List<OpenSourceDocument> LoadDocumentsFromFile()
        {
            var paths = GetJsonPaths();
            return paths
                .Select(ToDocument)
                .ToList();

        }

        public static OpenSourceDocument ToDocument(string path)
        {
            try
            {
                OpenSourceDocument document = JsonConvert.DeserializeObject<OpenSourceDocument>(File.ReadAllText(path));
                document.LocationOnDisk = Path.GetDirectoryName(path);
                return document;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Error reading file '{path}'", e);
            }
        }

        public static List<string> GetJsonPaths()
        {
            String path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OpenData");
            // We use .json.txt so the file opens in the system text editor without explanation.
            // This isn't ideal, but we're likley working with non-technical users outside their comfort zone,
            // and explaining file associations is not ideal

            return Directory.GetFiles(path, "*.json.txt", SearchOption.AllDirectories).ToList();
        }
    }

    public static class ClosedDataLoader
    {
        public static List<OpenSourceDocument> LoadDocumentsFromFile()
        {
            var paths = GetJsonPaths();
            return paths
                .Select(OpenDataLoader.ToDocument)
                .ToList();
        }

        public static List<string> GetJsonPaths()
        {
            String path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ClosedData");
            // We use .json.txt so the file opens in the system text editor without explanation.
            // This isn't ideal, but we're likley working with non-technical users outside their comfort zone,
            // and explaining file associations is not ideal

            return Directory.GetFiles(path, "*.json.txt", SearchOption.AllDirectories).ToList();
        }
    }



    /// <summary>
    /// A Document to be uploaded to the search - with additional properties regarding folder structure to allow for validation
    /// </summary>
    public class OpenSourceDocument : Document
    {
        /**
         * This abstracts a document (likely a bilingual text) that a user wants to upload: the main goal of this repository
         * We use JSON to define this structure: 
         *   * We don't want to use C#: a compile error will be difficult for a non-technical user to diagnose, and it'll break the Unit Tests
         * We use CSV to define the upload as it's both text-based and editable in Excel
         * 
         * We store these files in a folder structure with a minimum of:
         * * Document Text (CSV), License, Manifest (JSON)
         * 
         * We allow the folder structure so multiple CSV files can have identical names, to allow a logical structure to the documents
         *  and to allow relative paths when defining the PDF
         * This also makes it easy for us to allow each folder to contain additional notes on the document
         */

        public OpenSourceDocument()
        {
            CsvFileName = "document.csv";
        }


        public string LocationOnDisk { get; set; }

        public string FullCsvPath => Path.Combine(LocationOnDisk, CsvFileName);

        public string LicenseLink => Path.Combine(LocationOnDisk, "license.txt");

        internal override List<DocumentLine> LoadLocalFile()
        {
            return CsvHelperUtils.LoadCsv(FullCsvPath);
        }


        public override string ToString()
        {
            if (LocationOnDisk.StartsWith(AppDomain.CurrentDomain.BaseDirectory))
            {
                return LocationOnDisk.Substring(AppDomain.CurrentDomain.BaseDirectory.Length) + "\\manifest.json";
            }
            return LocationOnDisk + "\\manifest.json";
        }
    }
}