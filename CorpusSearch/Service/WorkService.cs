using Dapper;
using System.Threading.Tasks;

namespace Codex_API.Service
{
    public class WorkService
    {
        public static async Task<string> GetTitleFromIdent(string ident)
        {
            var workNameParam = new DynamicParameters();
            workNameParam.Add("ident", ident);
            return await Startup.conn.QuerySingleAsync<string>("SELECT name FROM works where ident = @ident", workNameParam);
        }
    }
}
