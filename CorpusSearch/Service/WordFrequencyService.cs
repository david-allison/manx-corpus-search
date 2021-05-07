using Codex_API.Model;
using Codex_API.Services;
using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Codex_API.Service
{
    public class WordFrequencyService
    {
        internal static void CreateTable(SqliteConnection conn)
        {
            // for each work, list all the words (normalized) and their count
            conn.ExecSql("create table wordfreq (" +
                "work int, " +
                "word varchar, " +
                "count int, " +
                "manx int, " +
                "FOREIGN KEY(work) REFERENCES works(id)" +
                ")");
        }

        internal static void AddDocument(int documentId, List<DocumentLine> validData)
        {

            var normalizedManxWords = validData.SelectMany(x => x.NormalizedManx.Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
               .ToLookup(x => x)
               .Select(x => new { Key = x.Key, Count = x.Count() })
               .OrderByDescending(x => x.Count)
               .ToList();
            Startup.conn.Execute("INSERT INTO [wordfreq] (work, word, count, manx) VALUES (@work, @word, @freq, 1)", normalizedManxWords.Select(x => new { work = documentId, word = x.Key, freq = x.Count }));

            var normalizedEnglishWords = validData.SelectMany(x => x.NormalizedEnglish.Split(" ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
               .ToLookup(x => x)
               .Select(x => new { Key = x.Key, Count = x.Count() })
               .OrderByDescending(x => x.Count)
               .ToList();
            Startup.conn.Execute("INSERT INTO [wordfreq] (work, word, count, manx) VALUES (@work, @word, @freq, 0)", normalizedEnglishWords.Select(x => new { work = documentId, word = x.Key, freq = x.Count }));
        }
    }
}
