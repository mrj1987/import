using System.Threading;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CSG.Common.ExtensionMethods;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StandardUtils;

using QueueReal = GenesysCloudDefQueueRealtime;

namespace GCRealTime
{
    public class QueueObsRealTime
    {
        public DataTable DTQueueDetails;
        public DataTable DTQueueConvActive;
        private DBUtils.DBUtils DBAdapter = new();
        private ChilkatSingleJson ChilKatSingleJsonObj = new();
        private string APIKey = string.Empty;
        private string JsonSearchString = string.Empty;
        private StringBuilder ActiveConversations = new StringBuilder();
        private DateTime QueueConvLastUpd = DateTime.UtcNow;
        private Boolean FirstConvUpdate = true;
        private int ErrorCounter = 0;
        private string CustomerKeyID = string.Empty;
        private string? TimeZoneConfig { get; set; }
        private DataTable? ClientFeatures { get; set; }
        private string ObsQuery = "/api/v2/analytics/queues/observations/query";
        private string ConvDetsQuery = "/api/v2/analytics/conversations/details/query";
        private string SingleConvDetsQuery = "/api/v2/conversations/";

        public QueueObsRealTime()
        {
        }

        public bool Initialize()
        {
            DBAdapter.Initialize();
            ChilKatSingleJsonObj.Initialize();
            APIKey = ChilKatSingleJsonObj.APIKey;

            DTQueueDetails = GetQueues();

            Utils UCAUtils = new Utils();
            CustomerKeyID = UCAUtils.ReadSetting("CSG_CUSTOMERKEYID");
            ClientFeatures = UCAUtils.GetGCCustomerConfig();
            TimeZoneConfig = Convert.ToString(ClientFeatures.Rows[0]["datetimezone"]);
            DTQueueConvActive = DBAdapter.CreateInMemTable("queuerealtimeconvData");
            return true;
        }

        public void runQueueObsData()
        {
            //CreateJSONSelectString();

            DateTime LastQueUpdDate = DateTime.Now;
            int Counter = 0;
            int OverCounter = 0;


            while (true)
            {
                Counter++;
                OverCounter++;
                Thread.Sleep(5000);
                bool Successful = false;

                #region Get All Queues
                if (Counter > 30)
                {
                    Console.Write("QO:Check Queues");
                    DTQueueDetails = GetQueues();
                    //CreateJSONSelectString();
                    Counter = 0;
                }
                #endregion

                #region Get new Api Key
                if (OverCounter > 7200)
                {
                    Console.Write("QO:Redo Key");
                    ChilKatSingleJsonObj.GetAuthAPIKey();
                    APIKey = ChilKatSingleJsonObj.APIKey;

                    OverCounter = 0;
                }

                #endregion

                #region Fire The Update Grab
                Console.WriteLine("\nQueue Obs Loop:{0}", DateTime.Now);
                getQueueStatus();
                #endregion

                #region Update Queue Data
                //var addOrUpdateRecords =
                //    (from drNew in DTQueueData.AsEnumerable()
                //     join drOld in DTQueueDetails.AsEnumerable() on drNew.Field<String>("id") equals drOld.Field<String>("id") into match
                //     from drOld in match.DefaultIfEmpty()
                //     where drOld == null /* new */ || !DataRowComparer.Default.Equals(drNew, drOld) /* changed */
                //     select drNew).ToList();


                //if (addOrUpdateRecords.Count > 0)
                //{
                //    Console.Write("QO:Upd Que Rec ");
                //    DTQueueDetails.AcceptChanges();
                //    Successful = DBAdapter.WriteSQLData(addOrUpdateRecords.CopyToDataTable(), "queueRealTimeData");
                //    DTQueueDetails = DTQueueData.Copy();
                //}
                //else
                //{
                //    Console.WriteLine("QO:No Que Upd ");
                //}

                if (DTQueueConvActive.Rows.Count > 0)
                {
                    Console.Write("QO:Upd Act Conv ");
                    DTQueueConvActive.AcceptChanges();

                    if (FirstConvUpdate == true)
                    {
                        Successful = DBAdapter.WriteSQLData(DTQueueConvActive, "queueconvrealData");
                        FirstConvUpdate = false;
                    }
                    else
                    {
                        var TempUpRows = DTQueueConvActive.Select("startdate > '" + QueueConvLastUpd + "'");

                        if (TempUpRows.Count() > 0)
                            Successful = DBAdapter.WriteSQLData(TempUpRows.CopyToDataTable(), "queueconvrealData");
                        else
                            Console.WriteLine("QO:No Que Conv Upd ");
                    }

                    if (ActiveConversations.Length > 0)
                    {
                        ActiveConversations.Length = ActiveConversations.Length - 3;
                        string DeleteString = "delete from queueconvrealData where keyid not in ('" + ActiveConversations + "')";
                        DBAdapter.ExecuteSQLQuery(DeleteString);
                    }

                    QueueConvLastUpd = DateTime.UtcNow;
                    DTQueueConvActive.Clear();
                }
                else
                    Console.WriteLine("QO:No Que Conv Upd ");
                #endregion

            }
        }

        private void CreateJSONSelectString(DataTable QDTempTable)
        {
            StringBuilder JSONSelect = new StringBuilder();

            foreach (DataRow DRQueue in QDTempTable.Rows)
            {
                JSONSelect.Append("{\"dimension\": \"queueId\",\"value\": \"" + DRQueue["id"] + "\"},");
            }

            JSONSelect.Length = JSONSelect.Length - 1;

            #region "SearchString"
            JsonSearchString = "{\"detailMetrics\": [ \"oWaiting\", \"oInteracting\"], " +
                                      "  \"metrics\": [ \"oWaiting\", \"oInteracting\"], " +
                                      "  \"filter\": { " +
                                      "    \"type\": \"and\", " +
                                      "    \"clauses\": [ " +
                                      "      { " +
                                      "        \"type\": \"or\", " +
                                      "        \"predicates\": [ " +
                                      JSONSelect.ToString() +
                                      "        ] " +
                                      "      } " +
                                      "    ] " +
                                      "  } " +
                                      "} ";
            #endregion

            //Console.WriteLine(JsonSearchString);
        }

        private string CreateJSONSingleQueueString(string QueueId)
        {
            string JsonSingleQueueSearchString = "{\"detailMetrics\": [ \"oWaiting\", \"oInteracting\"], " +
                                      "  \"metrics\": [ \"oWaiting\", \"oInteracting\"], " +
                                      "  \"filter\": { " +
                                      "    \"type\": \"and\", " +
                                      "    \"clauses\": [ " +
                                      "      { " +
                                      "        \"type\": \"or\", " +
                                      "        \"predicates\": [ " +
                                      "{\"dimension\": \"queueId\",\"value\": \"" + QueueId + "\"}" +
                                      "        ] " +
                                      "      } " +
                                      "    ] " +
                                      "  } " +
                                      "} ";

            return JsonSingleQueueSearchString;
        }

        #nullable enable
        private string CreateWaitingJsonSearchString(DataTable QDTempTable)
        {
            StringBuilder QueuesJson = new StringBuilder();

            foreach (DataRow DRQueue in QDTempTable.Rows)
            {
                QueuesJson.Append("{\"dimension\": \"queueId\",\"value\": \"" + DRQueue["id"] + "\"},");
            }

            string QueuesJsonString = QueuesJson.ToString().TrimEnd(',');

            string JsonSearchString = "{\"metrics\": [ \"oWaiting\", \"oInteracting\"], " +
                                      "  \"filter\": { " +
                                      "    \"type\": \"or\", " +
                                      "    \"clauses\": [ " +
                                      "      { " +
                                      "        \"type\": \"or\", " +
                                      "        \"predicates\": [ " +
                                      QueuesJsonString +
                                      "        ] " +
                                      "      } " +
                                      "    ] " +
                                      "  } " +
                                      "} ";

            return JsonSearchString;
        }

        private string CreateWaitingJSONSingleQueueString(string QueueId)
        {
            string JsonSingleQueueSearchString = "{\"metrics\": [ \"oWaiting\", \"oInteracting\"], " +
                                      "  \"filter\": { " +
                                      "    \"type\": \"and\", " +
                                      "    \"clauses\": [ " +
                                      "      { " +
                                      "        \"type\": \"or\", " +
                                      "        \"predicates\": [ " +
                                      "{\"dimension\": \"queueId\",\"value\": \"" + QueueId + "\"}" +
                                      "        ] " +
                                      "      } " +
                                      "    ] " +
                                      "  } " +
                                      "} ";

            return JsonSingleQueueSearchString;
        }
        #nullable restore

        #nullable enable
         private string CreateConvDetailsJsonSearchString(DateTime MinDate, DateTime MaxDate, string QueueId, int PageNumber)
        {
            JObject ConvDetailsJson = new JObject
            {
                {"interval", MinDate.ToString("s")+"Z" + "/" + MaxDate.ToString("s")+"Z"},
                {"order", "asc"},
                {"orderBy", "segmentEnd"},
                {"paging", new JObject
                    {
                        {"pageSize", "100"},
                        {"pageNumber", PageNumber}
                    }
                },
                {"segmentFilters", new JArray
                    {
                        new JObject
                        {
                            {"type", "and"},
                            {"predicates", new JArray
                                {
                                    // new JObject
                                    // {
                                    //     {"type", "dimension"},
                                    //     {"dimension", "purpose"},
                                    //     {"operator", "matches"},
                                    //     {"value", "acd"}
                                    // },
                                    // new JObject
                                    // {
                                    //     {"type", "dimension"},
                                    //     {"dimension", "segmentEnd"},
                                    //     {"operator", "notExists"},
                                    //     {"value", null}
                                    // },
                                    new JObject
                                    {
                                        {"type", "dimension"},
                                        {"dimension", "queueId"},
                                        {"operator", "matches"},
                                        {"value", QueueId}
                                    }
                                }
                            }
                        } 
                    }
                },
                {"conversationFilters", new JArray
                    {
                        new JObject
                        {
                            {"type", "and"},
                            {"predicates", new JArray
                                {
                                    new JObject
                                    {
                                        {"type", "dimension"},
                                        {"dimension", "conversationEnd"},
                                        {"operator", "notExists"},
                                        {"value", null}
                                    }
                                }
                            }
                        } 
                    }
                }
            };

            return ConvDetailsJson.ToString();
        }
        #nullable restore

        private DataTable GetQueues()
        {
            string SQLStatement = string.Empty;
            string TableName = "queueDetails";

            switch (DBAdapter.DBType)
            {
                case CSG.Adapter.Configuration.DatabaseType.MSSQL:
                case CSG.Adapter.Configuration.DatabaseType.MySQL:
                case CSG.Adapter.Configuration.DatabaseType.Snowflake:
                    SQLStatement = "SELECT * FROM " + TableName + " WHERE isactive";
                    break;
                case CSG.Adapter.Configuration.DatabaseType.PostgreSQL:
                    SQLStatement = "SELECT  * FROM " + TableName.ToLower() + " WHERE isactive";
                    break;
                default:
                    throw new NotImplementedException("Database type is not implemented");
            }

            DataTable Queues = DBAdapter.GetSQLTableData(SQLStatement, "QueueDetails");

            return Queues;

        }
        public void getQueueStatus()
        {
            TimeZoneInfo AppTimeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneConfig);

            string JsonString = string.Empty;
            #nullable enable
            bool IsTruncated = false;
            int PreTruncatedConvCounter = 0;

            string ConvDetailsJsonString = string.Empty;
            string ConvDetailsJsonSearchString = string.Empty;

            string SingleConvDetailsJsonString = string.Empty;

            int WaitingConvObjCount;

            string ConversationOutcome = "Not Added";

            int ProcessingConversationCounter = 0;
            #nullable restore

            QueueReal.QueueRealTime QueueData = new QueueReal.QueueRealTime();

            while(true)
            {
                try
                {
                    int MaxRowsToSend = 100;
                    int currentPage = 1;

                    int totalPages = 0;

                    if (DTQueueDetails.Rows.Count % MaxRowsToSend == 0)
                    {
                        Console.WriteLine("Reading Block of Data: Equal Division Pages is not adding one");
                        totalPages = (DTQueueDetails.Rows.Count / MaxRowsToSend);
                    }
                    else
                    {
                        Console.WriteLine("Reading Block of Data: Not Equal Division Pages adding one");
                        totalPages = (DTQueueDetails.Rows.Count / MaxRowsToSend) + 1;
                    }

                    ActiveConversations.Clear();
                    while (currentPage <= totalPages)
                    {
                        Console.WriteLine("Current Queue Page:{0}", currentPage);
                        // Ensure we have rows before trying to create a DataTable from them
                        DataTable dtTemp;
                        var rowsToProcess = DTQueueDetails.Rows.Cast<System.Data.DataRow>().Skip((currentPage - 1) * MaxRowsToSend).Take(MaxRowsToSend);
                        
                        if (rowsToProcess.Any())
                        {
                            dtTemp = rowsToProcess.CopyToDataTable();
                            dtTemp.TableName = DTQueueDetails.TableName;
                            CreateJSONSelectString(dtTemp);
                            
                            #nullable enable
                            JObject WaitingConvObj = JObject.Parse("{}");

                            // Output the number of conversations waiting in all the queues
                            WaitingConvObjCount = GetObsActiveConvCount(dtTemp);
                            Console.WriteLine("{0} QRT:: Number of active conversations in all queues {1}: ", DateTime.UtcNow, WaitingConvObjCount);
                            #nullable restore

                            List<string> AnalyticsConvIDStrings = new List<string>();
                            
                            // Rest of the processing for this page continues...
                        }
                        else
                        {
                            Console.WriteLine("{0} QRT:: No queue rows to process on page {1}", DateTime.UtcNow, currentPage);
                        }
                        
                        currentPage++;
                    }
                    ErrorCounter = 0;
                    break;
                }
                catch (Exception ex)
                {
                    ErrorCounter++;

                    // If error occurs in the Truncated steps, clear the database before throwing
                    if (ex.Message.Contains("Truncated Obs Error"))
                    {
                        DBAdapter.ExecuteSQLQuery("Delete from queueRealTimeConvData");
                        throw;
                    }

                    if (ErrorCounter > 3)
                    {
                        Console.WriteLine();
                        Console.WriteLine(DateTime.Now);
                        Console.WriteLine("QRT:: Error In Queue QC:Error Code :{0}", ex.ToString());
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine("QRT:: Inner Error:{0}", ex.InnerException.ToString());
                        }
                        Console.WriteLine("QRT:: Ending RealTime Application");
                        throw;
                    }
                    else
                    {
                        Console.WriteLine();
                        Console.WriteLine(DateTime.Now);
                        Console.WriteLine("QRT:: Error In Queue QC:Error Code :{0}", ex.ToString());
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine("QRT:: Inner Error:{0}", ex.InnerException.ToString());
                        }
                        Console.WriteLine("QRT:: Retrying RealTime Application");
                    }
                }
            }
            
        }



        public JObject GetAllAnalyticsConversations(string QueueID)
        {
            // Reset variables
            int PageNumber = 1;
            int monthLookback = 0;
            int queryInterval = 30;
            int totalMonthLookback = 0;

            int maxIntervalLookback = 1;
            int maxTotalLookback = 17;

            DateTime MaxObsDate = DateTime.UtcNow;
            DateTime MinObsDate = MaxObsDate.AddDays(-queryInterval);
            JObject ConversationDetailsObject = JObject.Parse("{}");
            JObject AllConversationDetailsObject  = JObject.Parse("{}");

            // Loop to get all conversation details for a queue
            while ( true )
            {
                // Query Genesys Cloud and return all the conversations which haven't ended for a given queue
                string ConvDetailsJsonSearchString = CreateConvDetailsJsonSearchString(MinObsDate, MaxObsDate, QueueID, PageNumber);
                string ConvDetailsJsonString = ChilKatSingleJsonObj.ReturnJson(ConvDetsQuery, ConvDetailsJsonSearchString);

                try
                {
                    ConversationDetailsObject = JObject.Parse(ConvDetailsJsonString);
                }
                catch (ArgumentNullException ex)
                {
                    throw new ArgumentNullException("Truncated Obs Error: No response was received from the following API Call: " + ConvDetsQuery, ex);
                }
                catch (Exception ex) when (ex.Message.Contains("Additional text"))
                {
                    throw new Exception("Truncated Obs Error - Failed API Call: " + ConvDetailsJsonString, ex);
                }

                if (ConversationDetailsObject != null && ConversationDetailsObject.Type != JTokenType.Null)
                { 
                    // Filter the Conversation Details object for the total hits property, representing the number of items which match the query
                    var TotalHits = ConversationDetailsObject.SelectToken("$.totalHits");

                    if (TotalHits != null && TotalHits.Type != JTokenType.Null)
                    {
                        //Console.WriteLine("Total Hits from the Conversation Details Query: {0} for Queue: {1}", TotalHits, QueueID);
                        //  If total hits is 0, got all conversations
                        if (TotalHits.ToObject<int>() == 0)
                        {
                            if(monthLookback > maxIntervalLookback || totalMonthLookback == maxTotalLookback)
                            {
                                break;
                            }
                            else
                            {
                                MaxObsDate = MinObsDate;
                                MinObsDate = MaxObsDate.AddDays(-queryInterval);
                                PageNumber = 1;
                                monthLookback++;
                                totalMonthLookback++;
                                Console.WriteLine("QRT:: Min Obs Date: {0}, Max Obs Date: {1}, Total Hits: {2}, Queue ID: {3}", MinObsDate, MaxObsDate, TotalHits, QueueID);
                                continue;
                            }
                        }
                        //  Else if total hits is greater than 100, get next page
                        else if (TotalHits.ToObject<int>() > 100*PageNumber)
                        {
                            PageNumber++;
                            //Console.WriteLine("Total ConvDet Hits over 100, get next page");
                        }
                        //  Else, calculate next interval and reset page number
                        else
                        {
                            MaxObsDate = MinObsDate;
                            MinObsDate = MaxObsDate.AddDays(-queryInterval);
                            PageNumber = 1;
                            totalMonthLookback++;
                            monthLookback = 0;
                            //Console.WriteLine("Total ConvDet Hits less than 100, check next interval");
                        }

                        //  store conversation details
                        AllConversationDetailsObject.Merge(ConversationDetailsObject);
                        Console.WriteLine("QRT:: Min Obs Date: {0}, Max Obs Date: {1}, Total Hits: {2}, Queue ID: {3}", MinObsDate, MaxObsDate, TotalHits, QueueID);
                    }
                    else
                    {
                        //Console.WriteLine("Total ConvDet Hits: {0} for Queue: {1}", "Null", QueueID);
                        break;
                    }
                }
            }

            return AllConversationDetailsObject;
        }

        public int GetObsActiveConvCount(DataTable dtTemp)
        {
            JObject WaitingConvObj = JObject.Parse("{}");

            // Output the number of conversations waiting in all the queues
            string WaitingJsonSearchString = CreateWaitingJsonSearchString(dtTemp);
            string WaitJsonString = ChilKatSingleJsonObj.ReturnJson(ObsQuery, WaitingJsonSearchString);
            QueueReal.QueueRealTime? WaitingQueueData = JsonConvert.DeserializeObject<QueueReal.QueueRealTime>(WaitJsonString,
            new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
            
            StringBuilder WaitingData = new StringBuilder();
            WaitingData.AppendLine();
            if (WaitingQueueData != null){
                if (WaitingQueueData.results != null)
                {
                    foreach(QueueReal.Result QueueDataRes in WaitingQueueData.results){
                        if (QueueDataRes.data != null && QueueDataRes.group != null){
                            foreach(QueueReal.Datum QueueDataResData in QueueDataRes.data)
                            {
                                if(QueueDataResData.stats != null){
                                    if(QueueDataResData.stats.count>0){
                                        WaitingData.Append("QueueID: "+QueueDataRes.group.queueId+ ", "+QueueDataResData.metric+" Count: "+QueueDataResData.stats.count);
                                        WaitingData.AppendLine();
                                    }
                                }
                            }
                        }
                    }
                }
            }
            //Console.WriteLine(WaitingData.ToString());

            try
            {
                WaitingConvObj = JObject.Parse(WaitJsonString);
            }
            catch (ArgumentNullException ex)
            {
                throw new ArgumentNullException("Truncated Obs Error: No response was received from the following API Call: " + ObsQuery, ex);
            }
            catch (Exception ex) when (ex.Message.Contains("Additional text"))
            {
                throw new Exception("Truncated Obs Error - Failed API Call: " + WaitJsonString, ex);
            }
            
            var WaitingConvObjCount = WaitingConvObj.SelectTokens("$..count").Sum(x=>((int)x));
            return WaitingConvObjCount;
        }

        public int GetSingleObsActiveConvCount(string QueueID)
        {
            JObject WaitingConvObj = JObject.Parse("{}");

            // Output the number of conversations waiting in all the queues
            string WaitingJsonSearchString = CreateWaitingJSONSingleQueueString(QueueID);
            string WaitJsonString = ChilKatSingleJsonObj.ReturnJson(ObsQuery, WaitingJsonSearchString);
            QueueReal.QueueRealTime? WaitingQueueData = JsonConvert.DeserializeObject<QueueReal.QueueRealTime>(WaitJsonString,
            new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
            
            StringBuilder WaitingData = new StringBuilder();
            WaitingData.AppendLine();
            if (WaitingQueueData != null){
                if (WaitingQueueData.results != null)
                {
                    foreach(QueueReal.Result QueueDataRes in WaitingQueueData.results){
                        if (QueueDataRes.data != null && QueueDataRes.group != null){
                            foreach(QueueReal.Datum QueueDataResData in QueueDataRes.data)
                            {
                                if(QueueDataResData.stats != null){
                                    if(QueueDataResData.stats.count>0){
                                    WaitingData.Append("QueueID: "+QueueDataRes.group.queueId+ ", "+QueueDataResData.metric+" Count: "+QueueDataResData.stats.count);
                                    WaitingData.AppendLine();
                                    }
                                }
                            }
                        }
                    }
                }
            }
            //Console.WriteLine(WaitingData.ToString());

            try
            {
                WaitingConvObj = JObject.Parse(WaitJsonString);
            }
            catch (ArgumentNullException ex)
            {
                throw new ArgumentNullException("Truncated Obs Error: No response was received from the following API Call: " + ObsQuery, ex);
            }
            catch (Exception ex) when (ex.Message.Contains("Additional text"))
            {
                throw new Exception("Truncated Obs Error - Failed API Call: " + WaitJsonString, ex);
            }
            
            var WaitingConvObjCount = WaitingConvObj.SelectTokens("$..count").Sum(x=>((int)x));
            return WaitingConvObjCount;
        }
        public void AddConversations(List<string> ConversationIds,string QueueID, string MediaType, TimeZoneInfo AppTimeZone, int ConvIDsCount)
        {
            List<(string ConversationId, string EndTime)> conversationList = new List<(string ConversationId, string EndTime)>();

            foreach(var convID in ConversationIds)
            {
                if (MediaType == "voice")
                {
                    MediaType = "call";
                }
                string JsonString = ChilKatSingleJsonObj.ReturnJson($"/api/v2/conversations/{MediaType}s/{convID}");
                if (!string.IsNullOrEmpty(JsonString) && JsonString != "{}" && JsonString != "")
                {
                    JObject jsonObject = JObject.Parse(JsonString);
                    var filteredParticipants = jsonObject.SelectTokens("$.participants[?(@.state == 'connected' && @.purpose != 'customer')]");
                    var firstParticipant = filteredParticipants.FirstOrDefault();
                    if (firstParticipant != null)
                    {
                        var endTime = firstParticipant["endTime"]?.ToString();
                        if (!string.IsNullOrEmpty(endTime))
                        {
                            conversationList.Add((convID, endTime));
                        }
                    }
                }

            }
            int ProcessingConversationCounter=0;
            lock(DTQueueConvActive)
            {
                if (ConversationIds.Count > 0)
                {
                    int batchSize = 100;

                    int numBatches = (int)Math.Ceiling((double)ConversationIds.Count / batchSize);
                    for (int batchIndex = 0; batchIndex < numBatches; batchIndex++)
                    {
                        List<string> batchConversationIds = ConversationIds.Skip(batchIndex * batchSize).Take(batchSize).ToList();

                        string ConversationIdsString = string.Join(",", batchConversationIds);
                        try
                        {
                            string JsonString = ChilKatSingleJsonObj.ReturnJson("/api/v2/analytics/conversations/details?id=" + ConversationIdsString);
                            if (!string.IsNullOrEmpty(JsonString) && JsonString != "{}" && JsonString != "")
                            {
                                dynamic jsonObject = JsonConvert.DeserializeObject(JsonString);

                                JArray conversations = jsonObject["conversations"];

                                foreach (JToken SingleConversationDetailsObject in conversations)
                                {
                                    string ConvID = SingleConversationDetailsObject["conversationId"].ToString();
                                    DataRow[] matchingRows = DTQueueConvActive.Select("conversationid = '" + ConvID + "'");
                                    if (matchingRows.Length > 0)
                                    {
                                        continue;
                                    }
                                    JToken EndTime = SingleConversationDetailsObject.SelectToken("$.conversationEnd");
                                    
                                    if (EndTime != null)
                                    {
                                        Console.WriteLine("{0} QRT:: {1}/{2} Conversation ID: {3} from Conversation Details Query for Queue ID: {4} has already ended on {5}", DateTime.UtcNow, ProcessingConversationCounter, ConvIDsCount, ConvID, QueueID, EndTime);
                                        continue;
                                        // ConversationOutcome = "Already Ended";
                                        // return ConversationOutcome;
                                    }
                                    DataRow Conversation = DTQueueConvActive.NewRow();
                                    string RowKey = ConvID + "|" + QueueID;
                                    Conversation["keyid"] = RowKey;
                                    Conversation["conversationid"] = SingleConversationDetailsObject["conversationId"].ToString();

                                    //Conversation["conversationid"] = ConvID;
                                    Conversation["queueid"] = QueueID;
                                    // Conversation["talktime"] = SingleConversationDetailsObject["conversationStart"];
                                    // Conversation["startdate"] = SingleConversationDetailsObject["conversationStart"];

                                    // var StartTimeLTC = SingleConversationDetailsObject["conversationStart"].ToObject<DateTime>();

                                    // Conversation["talktimeltc"] = TimeZoneInfo.ConvertTimeFromUtc(StartTimeLTC, AppTimeZone);
                                    // Conversation["startdateltc"] = TimeZoneInfo.ConvertTimeFromUtc(StartTimeLTC, AppTimeZone);

                                    // Filter the Conversation Details object for all the participants with a purpose equal to 'acd'
                                    //IEnumerable<JToken>AcdParticipants = SingleConversationDetailsObject.SelectTokens("$..participants[?(@.purpose == 'acd')]");
                                    IEnumerable<JToken>AcdParticipants = SingleConversationDetailsObject.SelectTokens("$..participants");
                                    JToken AcdParticipant = AcdParticipants.Values().Last();
                                    
                                    // Extract all the child properties for the participant
                                    IEnumerable<JProperty> AcdParticipantProps = AcdParticipant.Children().OfType<JProperty>();

                                    string Purpose = "";

                                    foreach(JProperty AcdProperty in AcdParticipantProps)
                                    {
                                        switch(AcdProperty.Name)
                                        {
                                           
                                            case "participantName":
                                                var Name = AcdProperty.Value.ToObject<string>();
                                                if (Name != null){
                                                    Conversation.SetFieldValue(RowKey, "participantname", Name);
                                                }
                                                break;
                                    
                                            case "sessions":
                                                if(AcdProperty.Value.Count() > 0)
                                                {
                                                    var Direction = AcdProperty.Value.Last()["direction"];
                                                    if (Direction != null)
                                                    {
                                                        Conversation["direction"] = Direction;
                                                    }

                                                    if (MediaType == null)
                                                    {
                                                        var Media = AcdProperty.Value.Last()["media"];
                                                        Conversation["media"] = MediaType;
                                                    }
                                                    else
                                                    {
                                                        Conversation["media"] = MediaType;
                                                    }
                                                    var Ani = AcdProperty.Value.Last()["ani"];
                                                    if (Ani != null)
                                                    {
                                                        Conversation["ani"] = Ani;
                                                    }
                                                    var Dnis = AcdProperty.Value.Last()["dnis"];
                                                    if (Dnis != null)
                                                    {
                                                        Conversation["dnis"] = Ani;
                                                    }
                                                    var Skills = AcdProperty.Value.Last()["skills"];
                                                    if (Skills != null)
                                                    {
                                                        int counter = 1;
                                                        foreach(var Skill in Skills){
                                                            Conversation["skill"+(counter)] = Skill;
                                                            counter++;
                                                        }
                                                    }
                                                    var segment = AcdProperty.Value.Last()["segments"];
                                                    if(segment!=null)
                                                    {
                                                        var segmentStart = segment.Last()["segmentStart"];
                                                        Conversation["talktime"] = segmentStart;
                                                        Conversation["startdate"] = segmentStart;

                                                        var StartTimeLTC =segmentStart.ToObject<DateTime>();

                                                        Conversation["talktimeltc"] = TimeZoneInfo.ConvertTimeFromUtc(StartTimeLTC, AppTimeZone);
                                                        Conversation["startdateltc"] = TimeZoneInfo.ConvertTimeFromUtc(StartTimeLTC, AppTimeZone);
                                                    }
                                                }
                                                break;

                                            case "purpose":
                                                Purpose = AcdProperty.Value.ToString();
                                                Conversation["actingas"] = Purpose;
                                                break;

                                        

                                            default:
                                                break;
                                        }   
                                    }

                                    Conversation["conversationstate"] = "connected";
                                    Conversation["userid"] = AcdParticipant["userid"];
                                    Conversation["updated"] = DateTime.UtcNow;

                                    DTQueueConvActive.Rows.Add(Conversation);
                                    ProcessingConversationCounter++;
                                    Console.WriteLine("{0} QRT:: {1}/{2} Conversation ID: {3} from Conversation Details Query with Purpose: {4} added for Queue ID: {5}", DateTime.UtcNow, ProcessingConversationCounter, ConvIDsCount, ConvID, Purpose, QueueID);
                                }
                            }
                         
                        }
                        catch(Exception ex)
                        {
                            throw new Exception("Failed API Call: /api/v2/analytics/conversations/details for conversations ", ex);
                        }     
                    }
                }
                foreach (var item in conversationList)
                {
                    var rowsToDelete = DTQueueConvActive.AsEnumerable()
                        .Where(row => row.Field<string>("ConversationId") == item.ConversationId)
                        .ToList();

                    foreach (var row in rowsToDelete)
                    {
                        DTQueueConvActive.Rows.Remove(row);
                    }
                }                  
            }
        } 
        public string AddConversation(string ConvID, string QueueID, string MediaType, TimeZoneInfo AppTimeZone, int ProcessingConversationCounter, int ConvIDsCount)
        {
            string ConversationOutcome = "Not Added";

            lock (DTQueueConvActive)
            {
                // Add the conversation if it is not already in the table
                if (DTQueueConvActive.Select("keyid = '" + ConvID + "|" + QueueID + "'").Count()==0)
                {
                    string SingleConvDetailsJsonString = string.Empty;
                    JObject SingleConversationDetailsObject  = JObject.Parse("{}");

                    // Call an API to get further details on this specific conversation
                    SingleConvDetailsJsonString = ChilKatSingleJsonObj.ReturnJson(SingleConvDetsQuery+ConvID);

                    try
                    {
                        SingleConversationDetailsObject = JObject.Parse(SingleConvDetailsJsonString);
                    }
                    catch (ArgumentNullException ex)
                    {
                        throw new ArgumentNullException("Truncated Obs Error: No response was received from the following API Call: "+SingleConvDetsQuery+ConvID, ex);
                    }
                    catch (Exception ex) when (ex.Message.Contains("Additional text"))
                    {
                        throw new Exception("Truncated Obs Error - Failed API Call: " + SingleConvDetailsJsonString, ex);
                    }

                    if (SingleConversationDetailsObject != null && SingleConversationDetailsObject.Type != JTokenType.Null)
                    {
                        // Check if the conversation has ended, do not process if ended
                        JToken EndTime = SingleConversationDetailsObject.SelectToken("$.endTime");
                        if (EndTime != null)
                        {
                            Console.WriteLine("{0} QRT:: {1}/{2} Conversation ID: {3} from Conversation Details Query for Queue ID: {4} has already ended on {5}", DateTime.UtcNow, ProcessingConversationCounter, ConvIDsCount, ConvID, QueueID, EndTime);
                            ConversationOutcome = "Already Ended";
                            return ConversationOutcome;
                        }

                        DataRow Conversation = DTQueueConvActive.NewRow();
                        string RowKey = ConvID + "|" + QueueID;
                        Conversation["keyid"] = RowKey;
                        Conversation["conversationid"] = ConvID;
                        Conversation["queueid"] = QueueID;

                        // Filter the Conversation Details object for all the participants with a purpose equal to 'acd'
                        //IEnumerable<JToken>AcdParticipants = SingleConversationDetailsObject.SelectTokens("$..participants[?(@.purpose == 'acd')]");
                        IEnumerable<JToken>AcdParticipants = SingleConversationDetailsObject.SelectTokens("$..participants");
                        JToken AcdParticipant = AcdParticipants.Values().Last();
                        
                        // Extract all the child properties for the participant
                        IEnumerable<JProperty> AcdParticipantProps = AcdParticipant.Children().OfType<JProperty>();

                        string Purpose = "";

                        foreach(JProperty AcdProperty in AcdParticipantProps)
                        {
                            // Based on the Participant Property Name, tranform and set conversation details
                            switch(AcdProperty.Name)
                            {
                                case "startTime":
                                    Conversation["talktime"] = AcdProperty.Value;
                                    Conversation["startdate"] = AcdProperty.Value;

                                    var StartTimeLTC = AcdProperty.Value.ToObject<DateTime>();

                                    Conversation["talktimeltc"] = TimeZoneInfo.ConvertTimeFromUtc(StartTimeLTC, AppTimeZone);
                                    Conversation["startdateltc"] = TimeZoneInfo.ConvertTimeFromUtc(StartTimeLTC, AppTimeZone);
                                    break;

                                case "name":
                                    var Name = AcdProperty.Value.ToObject<string>();
                                    if (Name != null){
                                        Conversation.SetFieldValue(RowKey, "participantname", Name);
                                    }
                                    break;

                                case "emails":
                                case "calls":
                                case "callbacks":
                                case "chats":
                                case "messages":
                                case "cobrowsesessions":
                                case "screenshares":
                                    if(AcdProperty.Value.Count() > 0)
                                    {
                                        var Direction = AcdProperty.Value.Last()["direction"];
                                        if (Direction != null)
                                        {
                                            Conversation["direction"] = Direction;
                                        }

                                        if (MediaType == null)
                                        {
                                            if (AcdProperty.Name == "calls")
                                            {
                                                Conversation["media"] = "voice";
                                            }
                                            if (AcdProperty.Name == "cobrowsesessions")
                                            {
                                                Conversation["media"] = "cobrowse";
                                            }
                                            else
                                            {
                                                Conversation["media"] = AcdProperty.Name.TrimEnd('s');
                                            }
                                        }
                                        else
                                        {
                                            Conversation["media"] = MediaType;
                                        }
                                    }
                                    break;

                                case "purpose":
                                    Purpose = AcdProperty.Value.ToString();
                                    Conversation["actingas"] = Purpose;
                                    break;

                                case "ani":
                                    var Ani = AcdProperty.Value.ToObject<string>();
                                    if (Ani != null)
                                    {
                                        Conversation.SetFieldValue(RowKey, "ani", Ani);
                                    }
                                    break;

                                case "dnis":
                                    var Dnis = AcdProperty.Value.ToObject<string>();
                                    if (Dnis != null)
                                    {
                                        Conversation.SetFieldValue(RowKey, "dnis", Dnis);
                                    }
                                    break;

                                case "conversationRoutingData":
                                    var Skills = AcdProperty.Value["skills"];
                                    if (Skills != null)
                                    {
                                        int counter = 1;
                                        foreach(var Skill in Skills){
                                            Conversation["skill"+(counter)] = Skill;
                                            counter++;
                                        }
                                    }
                                    break;

                                default:
                                    break;
                            }   
                        }

                        Conversation["conversationstate"] = "connected";
                        Conversation["userid"] = AcdParticipant["id"];
                        Conversation["updated"] = DateTime.UtcNow;

                        DTQueueConvActive.Rows.Add(Conversation);
                        ConversationOutcome = "Added";

                        Console.WriteLine("{0} QRT:: {1}/{2} Conversation ID: {3} from Conversation Details Query with Purpose: {4} added for Queue ID: {5}", DateTime.UtcNow, ProcessingConversationCounter, ConvIDsCount, ConvID, Purpose, QueueID);
                    }
                }
                else
                {
                    Console.WriteLine("{0} QRT:: {1}/{2} Conversation ID: {3} from Conversation Details Query already in Table for Queue ID: {4}", DateTime.UtcNow, ProcessingConversationCounter, ConvIDsCount, ConvID, QueueID);
                }
            }
            return ConversationOutcome;
        }

        public bool AddObsConversation(QueueReal.Observation ConversationDetail, string Metric, string QueueID, string MediaType, TimeZoneInfo AppTimeZone, int ProcessingConversationCounter, int ConvIDsCount)
        {
            bool ObsConversationAdded = false;

            ActiveConversations.Append(ConversationDetail.conversationId + "|" + QueueID + "','");

            lock (DTQueueConvActive)
            {
                if (DTQueueConvActive.Select("keyid = '" + ConversationDetail.conversationId + "|" + QueueID + "'").Count() == 0)
                {
                    DataRow Conversation = DTQueueConvActive.NewRow();
                    Conversation["keyid"] = ConversationDetail.conversationId + "|" + QueueID;

                    Conversation["conversationid"] = ConversationDetail.conversationId;

                    switch (Metric)
                    {
                        case "oInteracting":
                            Conversation["conversationstate"] = "connected";
                            Conversation["actingas"] = "agent";
                            Conversation["talktime"] = ConversationDetail.observationDate;
                            Conversation["talktimeltc"] = TimeZoneInfo.ConvertTimeFromUtc(ConversationDetail.observationDate, AppTimeZone);
                            Conversation["startdate"] = ConversationDetail.observationDate;
                            Conversation["startdateltc"] = TimeZoneInfo.ConvertTimeFromUtc(ConversationDetail.observationDate, AppTimeZone);
                            break;
                        case "oWaiting":
                            Conversation["conversationstate"] = "connected";
                            Conversation["actingas"] = "acd";
                            Conversation["talktime"] = ConversationDetail.observationDate;
                            Conversation["talktimeltc"] = TimeZoneInfo.ConvertTimeFromUtc(ConversationDetail.observationDate, AppTimeZone);
                            Conversation["startdate"] = ConversationDetail.observationDate;
                            Conversation["startdateltc"] = TimeZoneInfo.ConvertTimeFromUtc(ConversationDetail.observationDate, AppTimeZone);
                            break;
                    }

                    Conversation["queueid"] = QueueID;
                    Conversation["userid"] = ConversationDetail.userId;
                    Conversation["media"] = MediaType;
                    Conversation["initialpriority"] = ConversationDetail.routingPriority;
                    Conversation["direction"] = ConversationDetail.direction;
                    try
                    {
                        if (ConversationDetail.participantName != null)
                        {
                            if (ConversationDetail.participantName.Length > 149)
                                Conversation["participantname"] = ConversationDetail.participantName.Substring(0, 150);
                            else
                                Conversation["participantname"] = ConversationDetail.participantName;
                        }

                        if (ConversationDetail.ani != null)
                        {
                            if (ConversationDetail.ani.Length > 300)
                                Conversation["ani"] = ConversationDetail.ani.Substring(0, 300);
                            else
                                Conversation["ani"] = ConversationDetail.ani;
                        }

                        if (ConversationDetail.dnis != null)
                        {
                            if (ConversationDetail.dnis.Length > 300)
                                Conversation["dnis"] = ConversationDetail.dnis.Substring(0, 300);
                            else
                                Conversation["dnis"] = ConversationDetail.dnis;
                        }

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Non Fatal Error: {0}", ex.ToString());
                    }
                    Conversation["usedrout"] = ConversationDetail.usedRouting;

                    if (ConversationDetail.requestedRoutingSkillIds != null)
                    {
                        if (ConversationDetail.requestedRoutingSkillIds.Count() > 0)
                            Conversation["skill1"] = ConversationDetail.requestedRoutingSkillIds[0];
                        if (ConversationDetail.requestedRoutingSkillIds.Count() > 1)
                            Conversation["skill2"] = ConversationDetail.requestedRoutingSkillIds[1];
                        if (ConversationDetail.requestedRoutingSkillIds.Count() > 2)
                            Conversation["skill3"] = ConversationDetail.requestedRoutingSkillIds[2];
                    }

                    if (ConversationDetail.requestedRoutings != null)
                    {
                        if (ConversationDetail.requestedRoutings.Count() > 0)
                            Conversation["requestedrout1"] = ConversationDetail.requestedRoutings[0];
                        if (ConversationDetail.requestedRoutings.Count() > 1)
                            Conversation["requestedrout2"] = ConversationDetail.requestedRoutings[1];
                    }

                    Conversation["updated"] = DateTime.UtcNow;
                    DTQueueConvActive.Rows.Add(Conversation);
                    Console.WriteLine("{0} QRT:: {1}/{2} Conversation ID: {3} from Observations Query added for Queue ID: {4}", DateTime.UtcNow, ProcessingConversationCounter, ConvIDsCount, ConversationDetail.conversationId, QueueID);
                    ObsConversationAdded = true;
                }
                else
                {
                    DataRow Conversation = DTQueueConvActive.Select("keyid = '" + ConversationDetail.conversationId + "|" + QueueID + "'").FirstOrDefault();
                    Conversation["conversationid"] = ConversationDetail.conversationId;
                    switch (Metric)
                    {
                        case "oInteracting":
                            Conversation["conversationstate"] = "connected";
                            Conversation["actingas"] = "agent";
                            Conversation["talktime"] = ConversationDetail.observationDate;
                            Conversation["talktimeltc"] = TimeZoneInfo.ConvertTimeFromUtc(ConversationDetail.observationDate, AppTimeZone);
                            Conversation["startdate"] = ConversationDetail.observationDate;
                            Conversation["startdateltc"] = TimeZoneInfo.ConvertTimeFromUtc(ConversationDetail.observationDate, AppTimeZone);
                            break;
                        case "oWaiting":
                            Conversation["conversationstate"] = "connected";
                            Conversation["actingas"] = "acd";
                            Conversation["talktime"] = ConversationDetail.observationDate;
                            Conversation["talktimeltc"] = TimeZoneInfo.ConvertTimeFromUtc(ConversationDetail.observationDate, AppTimeZone);
                            Conversation["startdate"] = ConversationDetail.observationDate;
                            Conversation["startdateltc"] = TimeZoneInfo.ConvertTimeFromUtc(ConversationDetail.observationDate, AppTimeZone);
                            break;
                    }
                    Conversation["queueid"] = QueueID;
                    Conversation["media"] = MediaType;
                    Conversation["initialpriority"] = ConversationDetail.routingPriority;
                    if (ConversationDetail.participantName != null)
                        Conversation["participantname"] = ConversationDetail.participantName;
                    Conversation["direction"] = ConversationDetail.direction;
                    Conversation["ani"] = ConversationDetail.ani;
                    Conversation["dnis"] = ConversationDetail.dnis;
                    Conversation["usedrout"] = ConversationDetail.usedRouting;

                    if (ConversationDetail.requestedRoutingSkillIds != null)
                    {
                        if (ConversationDetail.requestedRoutingSkillIds.Count() > 0)
                            Conversation["skill1"] = ConversationDetail.requestedRoutingSkillIds[0];
                        if (ConversationDetail.requestedRoutingSkillIds.Count() > 1)
                            Conversation["skill2"] = ConversationDetail.requestedRoutingSkillIds[1];
                        if (ConversationDetail.requestedRoutingSkillIds.Count() > 2)
                            Conversation["skill3"] = ConversationDetail.requestedRoutingSkillIds[2];
                    }

                    if (ConversationDetail.requestedRoutings != null)
                    {
                        if (ConversationDetail.requestedRoutings.Count() > 0)
                            Conversation["requestedrout1"] = ConversationDetail.requestedRoutings[0];
                        if (ConversationDetail.requestedRoutings.Count() > 1)
                            Conversation["requestedrout2"] = ConversationDetail.requestedRoutings[1];
                    }

                    Conversation["updated"] = DateTime.UtcNow;
                    Console.WriteLine("{0} QRT:: {1}/{2} Conversation ID: {3} from Observations Query already in Table for Queue ID: {4}", DateTime.UtcNow, ProcessingConversationCounter, ConvIDsCount, ConversationDetail.conversationId, QueueID);
                }
            }

            return ObsConversationAdded;
        }
    }
}
