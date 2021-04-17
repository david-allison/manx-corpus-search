using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Codex_API.Services
{
    public static class SqlExtensions
    {
        public static void ExecSql(this SqliteConnection sql_con, String command)
        {
            var sql_cmd = sql_con.CreateCommand();
            sql_cmd.CommandText = command;
            sql_cmd.ExecuteNonQuery();
        }
    }
}
