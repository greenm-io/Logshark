using Logshark.PluginLib.Helpers;
using MongoDB.Bson;
using ServiceStack.DataAnnotations;
using System;

namespace Logshark.Plugins.OpenPerformance.Model
{
    class PerformanceData
    {
        [PrimaryKey]
        [AutoIncrement]
        public int Id { get; set; }

        [Index]
        public string Session { get; set; }

        public string RequestId { get; set; }

        public long TimeMs { get; set; }

        public long ResponseSize { get; set; }

        [Index]
        public string User { get; set; }

        public string Workbook { get; set; }

        [Index]
        public string Dashboard { get; set; }

        public string Site { get; set; }

        public DateTime? StartTs { get; set; }

    }
}
