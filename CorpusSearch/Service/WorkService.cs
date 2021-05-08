using Dapper;
using System.Threading.Tasks;

namespace Codex_API.Service
{
    public class WorkService
    {
        public static async Task<string> GetTitleFromIdent(string ident)
        {
            return await Startup.conn.QuerySingleAsync<string>("SELECT name FROM works where ident = @ident", new { ident });
        }
    }
}
