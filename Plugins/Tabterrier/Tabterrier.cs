using Logshark.ArtifactProcessors.TableauServerLogProcessor.Parsers;
using Logshark.ArtifactProcessors.TableauServerLogProcessor.PluginInterfaces;
using Logshark.PluginLib.Extensions;
using Logshark.PluginLib.Model.Impl;
using Logshark.PluginLib.Persistence;
using Logshark.PluginModel.Model;
using Logshark.Plugins.Tabterrier.Helpers;
using Logshark.Plugins.Tabterrier.Model;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Logshark.Plugins.Tabterrier
{
    /// <summary>
    /// Tabterrier Workbook Creation Plugin
    /// </summary>
    public class Tabterrier : BaseWorkbookCreationPlugin, IServerClassicPlugin, IServerTsmPlugin
    {
        private IPluginResponse pluginResponse;
        private string vizql = "vizqlserver_cpp";
        private string performance_result = "performance_result";
        private string vizql_users = "vizql_users";
        private string apache = "httpd";
        private AggregateOptions allowDiskUsageArg = new AggregateOptions { AllowDiskUse = true };

        private IPersister<PerformanceData> openPerformancePersister;

        public Tabterrier()
        {
        }

        public override ISet<string> CollectionDependencies
        {
            get
            {
                return new HashSet<string>
               {
                    ParserConstants.HttpdCollectionName,
                    ParserConstants.VizqlServerCppCollectionName
                };
            }
        }

        // List of embedded workbooks to publish at the end of the plugin execution.
        // These workbooks should all be set as "Embedded Resource" in Visual Studio.
        public override ICollection<string> WorkbookNames
        {
            get
            {
                return new List<string>
                {
                    "Tabterrier.twb"
                };
            }
        }

        public override IPluginResponse Execute(IPluginRequest pluginRequest)
        {
            this.pluginResponse = CreatePluginResponse();
            this.openPerformancePersister = GetConcurrentBatchPersister<PerformanceData>(pluginRequest);

            CollectUserData();
            EnrichHttpd();

            IMongoCollection<BsonDocument> openPerformanceResult = MongoDatabase.GetCollection<BsonDocument>(performance_result);

            using (GetPersisterStatusWriter(this.openPerformancePersister, CountRecords(openPerformanceResult)))
            {
                ProcessLogs(openPerformanceResult);
                this.openPerformancePersister.Shutdown();
            }

            Log.Info("Finished processing OpenPerformance requests!");

            // Check if we persisted any data.
            if (!PersistedData())
            {
                Log.Info("Failed to persist any data!");
                pluginResponse.GeneratedNoData = true;
            }

            return pluginResponse;
        }


        private void EnrichHttpd()
        {
            IMongoCollection<BsonDocument> apacheCollection = MongoDatabase.GetCollection<BsonDocument>(apache);

            FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Regex("resource", new BsonRegularExpression(".*bootstrapSession.*"));
            BsonDocument project = new BsonDocument {
                {"ts", 1 },
                {"resource", 1},
                {"request_time", 1},
                {"request_id", 1},
                {"response_size", 1},
                {"session", "$user_info.session"},
                {"user", "$user_info.user"},
                {"site", "$user_info.site"}
            };

            apacheCollection.Aggregate(allowDiskUsageArg)
                .Match(filter)
                .Lookup(vizql_users, "request_id", "_id", "user_info")
                .Unwind("user_info")
                .Project(project)
                .Out(performance_result);
        }

        private void CollectUserData()
        {
            IMongoCollection<BsonDocument> vizqlCollection = MongoDatabase.GetCollection<BsonDocument>(vizql);

            BsonDocument group = new BsonDocument {
                { "_id", "$req" },
                {"session", new BsonDocument("$max", "$sess")},
                {"user", new BsonDocument("$max", "$user")},
                {"site", new BsonDocument("$min", "$site")}
           };

            vizqlCollection.Aggregate(allowDiskUsageArg)
                .Group(group)
                .Out(vizql_users);
        }

        protected long CountRecords(IMongoCollection<BsonDocument> collection)
        {
            return collection.Count(new BsonDocument());
        }

        /// <summary>
        /// Processes data from performance aggregation result and persists results to DB.
        /// </summary>
        protected void ProcessLogs(IMongoCollection<BsonDocument> openPerformanceCollection)
        {
            GetOutputDatabaseConnection().CreateOrMigrateTable<PerformanceData>();

            IMongoCollection<BsonDocument> performanceData = MongoDatabase.GetCollection<BsonDocument>(performance_result);
            Log.Info("Queueing Performance records for processing..");

            var cursor = GetCursor(openPerformanceCollection);
            var tasks = new List<Task>();

            using (GetTaskStatusWriter(tasks, "Performance records processing", CountRecords(openPerformanceCollection)))
            {
                while (cursor.MoveNext())
                {
                    tasks.AddRange(cursor.Current.Select(document => Task.Factory.StartNew(() => ProcessRecord(document))));
                }
                Task.WaitAll(tasks.ToArray());
            }
        }

        /// <summary>
        /// Gets a cursor for the Performance collection.
        /// </summary>
        protected IAsyncCursor<BsonDocument> GetCursor(IMongoCollection<BsonDocument> collection)
        {
            return collection.Find(new BsonDocument()).ToCursor();
        }

        /// <summary>
        /// Populates the VizqlPerformance object and queues it for insertion.
        /// </summary>
        protected void ProcessRecord(BsonDocument document)
        {
            try
            {
                this.openPerformancePersister.Enqueue(PerformanceDataBuilder.Build(document));
            }
            catch (Exception ex)
            {
                string errorMessage = String.Format("Encountered an exception on {0}: {1}", document.GetValue("_id"), ex);
                pluginResponse.AppendError(errorMessage);
                Log.Error(errorMessage);
            }
        }
    }
}