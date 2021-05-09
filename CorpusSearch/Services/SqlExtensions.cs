using Microsoft.Data.Sqlite;
using System;

namespace CorpusSearch.Services
{
    public static class SqlExtensions
    {
        public static void ExecSql(this SqliteConnection sql_con, string command)
        {
            var sql_cmd = sql_con.CreateCommand();
            sql_cmd.CommandText = command;
            sql_cmd.ExecuteNonQuery();
        }
    }
}
