using Logshark.Plugins.Tabterrier.Model;
using MongoDB.Bson;
using System.Text.RegularExpressions;
using Logshark.PluginLib.Helpers;

namespace Logshark.Plugins.Tabterrier.Helpers
{
    class PerformanceDataBuilder
    {

        private static Regex regex = new Regex(".*\\/w\\/(?<workbookName>[^\\/]+).*\\/v\\/(?<dashboadName>[^\\/]+)");

        public static PerformanceData Build(BsonDocument logLine)
        {
            PerformanceData result = new PerformanceData();
           
            string resource = BsonDocumentHelper.GetString("resource", logLine);
            GroupCollection groups = regex.Match(resource).Groups;
            result.Workbook = groups["workbookName"].Value;
            result.Dashboard = groups["dashboadName"].Value;

            result.User = getUser(logLine);
            result.Session = BsonDocumentHelper.GetString("session", logLine);
            result.Site = BsonDocumentHelper.GetString("site", logLine);
            result.StartTs = BsonDocumentHelper.GetDateTime("ts", logLine);
            result.RequestId = BsonDocumentHelper.GetString("request_id", logLine);
            result.TimeMs = BsonDocumentHelper.GetLong("request_time", logLine);
            result.ResponseSize = BsonDocumentHelper.GetLong("response_size", logLine);
            return result;
        }

        private static string getUser(BsonDocument logLine)
        {
            string user = BsonDocumentHelper.GetString("user", logLine);
            if (user.Contains("\\"))
            {
                return user.Substring(user.IndexOf("\\") + 1);
            }
            else
            {
                return user;
            }
        }
    }
}
