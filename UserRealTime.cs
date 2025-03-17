using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Chilkat;
using Newtonsoft.Json;
using RealCN = RealUserPushConversations;
using RealUA = RealUserPushActivityDef;
using RealUC = RealUserPushCallStatsDef;
using StandardUtils;
using StringBuilder = System.Text.StringBuilder;
using UserReal = GenesysCloudDefUserRealtime;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace GCRealTime
{
    public class UserRealTime
    {
        const string APISockURL = "streaming.mypurecloud.com.au";
        public String SyncType { get; set; }
        private DBUtils.DBUtils DBAdapter = new DBUtils.DBUtils();
        private ChilKatJson ChilKatJsonObj = new ChilKatJson();
        private String APIKey = String.Empty;
        private String CustomerKeyID = String.Empty;
        private DataTable DTUserDetails = new DataTable();
        private DataTable DTQueueDetails = new DataTable();
        private DataTable DTQueueObservations = new DataTable();
        private DataTable DTUserData = new DataTable();
        private DataTable DTUserCallsDets = new DataTable();
        private DataTable DTQueueCallsDets = new DataTable();
        private WebSocketDetail GCWebSocketAct = new WebSocketDetail();
        private WebSocketDetail GCWebSocketAdh = new WebSocketDetail();
        private WebSocketDetail GCWebSocketCalls = new WebSocketDetail();
        private WebSocketDetail GCWebSocketCallDets = new WebSocketDetail();
        private WebSocketDetail GCWebSocketQueueCallDets = new WebSocketDetail();
        private WebSocketDetail GCWebSocketQueueObs = new WebSocketDetail();
        private System.Timers.Timer Process;
        private DateTime LastChannelUpd;
        private DateTime LastTermUpd;
        private Boolean WriteUserDataAct = false;
        private Boolean WriteUserDataAdh = false;
        private Boolean WriteUserDataCalls = false;
        private Boolean WriteUserDataCallsDets = false;
        private Boolean WriteQueueDataCallsDets = false;
        public int TotalErrors;
        public bool ShouldExit = false;


        private DataTable? ClientFeatures { get; set; }
        public string? TimeZoneConfig { get; set; }
        private readonly ILogger? _logger;
        private string JsonSearchString = string.Empty;

        private const int MAX_TOPICS_PER_SUBSCRIPTION = 1000;
        public List<Thread> WebSocketThreads = new List<Thread>();
        private System.Timers.Timer _channelRefreshTimer;
        private readonly object _channelLock = new object();
        private bool _isRefreshingChannels = false;

        // We need to track multiple WebSocket connections per function when we have more than 1,000 topics
        private List<WebSocketDetail> GCWebSocketActList = new List<WebSocketDetail>();
        private List<WebSocketDetail> GCWebSocketAdhList = new List<WebSocketDetail>();
        private List<WebSocketDetail> GCWebSocketCallsList = new List<WebSocketDetail>();
        private List<WebSocketDetail> GCWebSocketCallDetsList = new List<WebSocketDetail>();
        private List<WebSocketDetail> GCWebSocketQueueCallDetsList = new List<WebSocketDetail>();
        private List<WebSocketDetail> GCWebSocketQueueObsList = new List<WebSocketDetail>();

        public UserRealTime(ILogger logger)
        {
            _logger = logger;
        }

        public void Initialize()
        {
            Utils Utils = new Utils();

            Console.WriteLine("Starting MultiThread");

            DBAdapter.Initialize();
            ChilKatJsonObj.Initialize();

            CustomerKeyID = Utils.ReadSetting("CSG_CUSTOMERKEYID");

            ClientFeatures = Utils.GetGCCustomerConfig();

            APIKey = ChilKatJsonObj.APIKey;

            TimeZoneConfig = Convert.ToString(ClientFeatures.Rows[0]["datetimezone"]);

            //Boolean Successful = DBAdapter.ExecuteSQLQuery("Delete from userRealTimeData");

            LastChannelUpd = DateTime.Now;
            LastTermUpd = DateTime.Now;
            TotalErrors = 0;
          
            // Clear existing lists in case of reinitialization
            GCWebSocketActList.Clear();
            GCWebSocketAdhList.Clear();
            GCWebSocketCallsList.Clear();
            GCWebSocketCallDetsList.Clear();
            GCWebSocketQueueCallDetsList.Clear();
            GCWebSocketQueueObsList.Clear();
            WebSocketThreads.Clear();

            switch (SyncType)
            {
                case "userActivity":
                    ClearCacheTable("userRealTimeData");
                    DTUserDetails = GetUsers();
                    DTUserData = GetUserStatus();
                    if (DTUserData.Rows.Count > 0)
                    {
                        DBAdapter.WriteSQLDataBulk(DTUserData);
                    }
                    break;
                case "userCalls":
                    DTUserDetails = GetUsers();
                    DTUserCallsDets = DBAdapter.CreateInMemTable("userRealTimeConvData");
                    break;
                case "queueCalls":
                    DTQueueDetails = GetQueues();
                    DTQueueCallsDets = DBAdapter.CreateInMemTable("queueRealTimeConvData");
                    break;
                case "queueObservations":
                    DTQueueDetails = GetQueues();
                    DTQueueObservations = DBAdapter.CreateInMemTable("queueObservations");
                    break;
            }

            // Set up a timer to refresh channels every 20 hours to prevent expiry
            _channelRefreshTimer = new System.Timers.Timer(TimeSpan.FromHours(20).TotalMilliseconds);
            _channelRefreshTimer.Elapsed += ChannelRefreshTimer_Elapsed;
            _channelRefreshTimer.AutoReset = true;
            _channelRefreshTimer.Enabled = true;
        }

        private void ChannelRefreshTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (_isRefreshingChannels) return;

            lock (_channelLock)
            {
                if (_isRefreshingChannels) return;
                _isRefreshingChannels = true;

                try
                {
                    // Track all WebSocketDetail objects across all categories
                    List<WebSocketDetail> allChannels = new List<WebSocketDetail>();
                    allChannels.AddRange(GCWebSocketActList);
                    allChannels.AddRange(GCWebSocketAdhList);
                    allChannels.AddRange(GCWebSocketCallsList);
                    allChannels.AddRange(GCWebSocketCallDetsList);
                    allChannels.AddRange(GCWebSocketQueueCallDetsList);
                    
                    // Check each channel's expiration time
                    foreach (WebSocketDetail channel in allChannels)
                    {
                        if (channel.IsExpired)
                        {
                            _logger?.LogWarning($"Channel has expired: {channel}");
                            // The WebSocket will likely fail and be recreated
                        }
                        else if (channel.NeedsRefresh)
                        {
                            _logger?.LogInformation($"Refreshing channel that expires soon: {channel}");
                            RefreshSubscriptionsForChannel(channel, FindChunkForChannel(channel, DTUserDetails), channel.ReportName);
                        }
                        else
                        {
                            _logger?.LogDebug($"Channel status: {channel}");
                        }
                    }
                    
                    // Always refresh API key if it's getting old
                    if ((DateTime.Now - LastChannelUpd).TotalHours > 6)
                    {
                        _logger?.LogInformation("Refreshing API key");
                        if (ChilKatJsonObj.GetAuthAPIKey())
                        {
                            APIKey = ChilKatJsonObj.APIKey;
                            LastChannelUpd = DateTime.Now;
                            _logger?.LogInformation("API key refreshed successfully");
                        }
                        else
                        {
                            _logger?.LogWarning("Failed to refresh API key");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error refreshing channel subscriptions");
                }
                finally
                {
                    _isRefreshingChannels = false;
                }
            }
        }
        
        // Helper method to find the appropriate chunk for a channel
        private DataTable FindChunkForChannel(WebSocketDetail channel, DataTable allUsers)
        {
            if (channel.ReportName.Contains("-"))
            {
                string[] parts = channel.ReportName.Split('-');
                if (parts.Length > 1 && int.TryParse(parts[1], out int chunkIndex) && chunkIndex > 0)
                {
                    var chunks = ChunkDataTable(allUsers, MAX_TOPICS_PER_SUBSCRIPTION);
                    if (chunkIndex <= chunks.Count)
                    {
                        return chunks[chunkIndex - 1];
                    }
                }
            }
            
            // Default to the full table if we can't determine the chunk
            return allUsers;
        }
        
        // Updated method to refresh subscriptions for a specific channel
        private void RefreshSubscriptionsForChannel(WebSocketDetail webSocket, DataTable dataChunk, string subscriptionType)
        {
            if (webSocket == null || string.IsNullOrEmpty(webSocket.id)) return;

            try
            {
                _logger?.LogInformation($"Refreshing subscriptions for channel {webSocket.id} ({subscriptionType})");
                
                // Delete existing subscriptions
                string deleteUrl = $"/api/v2/notifications/channels/{webSocket.id}/subscriptions";
                ChilKatJsonObj.ReturnJson(deleteUrl, "", "DELETE");
                
                // Recreate subscriptions based on type
                switch (subscriptionType.Split('-')[0])
                {
                    case "userActivity":
                        CreateUserActivitySubsForChunk(webSocket, dataChunk);
                        break;
                    case "userAdherence":
                        CreateUserAdherenceSubsForChunk(webSocket, dataChunk);
                        break;
                    case "userCallStats":
                        CreateUserCallSubsForChunk(webSocket, dataChunk);
                        break;
                    case "userCallDets":
                        CreateUserCallDetSubsForChunk(webSocket, dataChunk);
                        break;
                    case "queueCallDets":
                        if (DTQueueDetails != null && DTQueueDetails.Rows.Count > 0)
                        {
                            DataTable queueChunk = FindChunkForChannel(webSocket, DTQueueDetails);
                            CreateQueueCallDetSubsForChunk(webSocket, queueChunk);
                        }
                        break;
                    default:
                        _logger?.LogWarning($"Unknown subscription type: {subscriptionType}");
                        break;
                }
                
                _logger?.LogInformation($"Successfully refreshed subscriptions for channel {webSocket.id} ({subscriptionType})");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to refresh subscriptions for {subscriptionType}");
            }
        }

        public void GetConversationStatus()
        {
            List<string> ConversationIds = new List<string>();
            List<int> rowsToUpdateIndexes = new List<int>();
            List<int> DeleteConversationRows = new List<int>();

            try
            {
                foreach (DataRow row in DTQueueCallsDets.Rows.Cast<DataRow>().ToList())
                {
                    // Null handling for startDate and updatedDate
                    if (row["startdate"] == DBNull.Value || row["updated"] == DBNull.Value)
                    {
                        continue;
                    }

                    DateTime startDate;
                    DateTime updatedDate;

                    if (!DateTime.TryParse(row["startdate"].ToString(), out startDate) ||
                        !DateTime.TryParse(row["updated"].ToString(), out updatedDate))
                    {
                        continue;
                    }

                    TimeSpan difference = updatedDate - startDate;
                    double differenceInMinutes = difference.TotalMinutes;

                    if (row["manuallychecked"] != DBNull.Value && Convert.ToBoolean(row["manuallychecked"]))
                    {
                        if (differenceInMinutes > 60 && row["media"].ToString() != "email")
                        {
                            row["manuallychecked"] = false;
                        }
                        else
                        {
                            continue;   
                        } 
                    }

                    if (differenceInMinutes > 5)
                    {
                        ConversationIds.Add(row["conversationid"].ToString());
                        rowsToUpdateIndexes.Add(DTQueueCallsDets.Rows.IndexOf(row));
                    }
                }

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
                            string JsonString = ChilKatJsonObj.ReturnJson("/api/v2/analytics/conversations/details?id=" + ConversationIdsString);

                            if (!string.IsNullOrEmpty(JsonString) && JsonString != "{}")
                            {
                                dynamic jsonObject = JsonConvert.DeserializeObject(JsonString);
                                JArray conversations = jsonObject["conversations"];

                                foreach (JToken conversation in conversations)
                                {
                                    string conversationId = conversation["conversationId"].ToString();

                                    for (int i = 0; i < rowsToUpdateIndexes.Count; i++)
                                    {
                                        int rowIndex = rowsToUpdateIndexes[i];
                                        if (rowIndex >= 0 && rowIndex < DTQueueCallsDets.Rows.Count)
                                        {
                                            DataRow row = DTQueueCallsDets.Rows[rowsToUpdateIndexes[i]];
                                            if (row["conversationid"].ToString() == conversationId)
                                            {
                                                row["manuallychecked"] = true;
                                                Console.WriteLine("{0} Conversation ID: {1} from Conversation Details Api has been manually checked", DateTime.UtcNow, conversationId);

                                                if (conversation["conversationEnd"] != null)
                                                {
                                                    row["conversationstate"] = "disconnected";
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error during API call for conversations: {0}\n{1}", ConversationIdsString, ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: {0}", ex.Message);
                throw;
            }
        }
        public void CreateWebSocket(string SocketAddress, string SocketChannel, string ThreadName)
        {
            Chilkat.Rest ChilRest = new Chilkat.Rest();
            Chilkat.WebSocket ChilWebSocket = new Chilkat.WebSocket();

            Process = new System.Timers.Timer(3000);
            Process.Elapsed += new System.Timers.ElapsedEventHandler(this.TimerCallBack);
            Process.AutoReset = true;
            Process.Enabled = true;

            // Create ping timer for WebSocket health checks
            System.Timers.Timer pingTimer = new System.Timers.Timer(30000); // 30 seconds
            pingTimer.Elapsed += (sender, e) => {
                SendWebSocketPing(ChilWebSocket);
            };
            pingTimer.AutoReset = true;
            pingTimer.Enabled = true;

            bool success = ChilRest.Connect(SocketAddress, 443, true, false);
            if (success != true)
            {
                _logger.LogError(ChilRest.LastErrorText);
                return;
            }

            success = ChilWebSocket.UseConnection(ChilRest);
            if (success != true)
            {
                _logger.LogError(ChilWebSocket.LastErrorText);
                return;
            }

            ChilWebSocket.AddClientHeaders();

            string ResponseBody = ChilRest.FullRequestNoBody("GET", SocketChannel);
            int responseCode = ChilRest.ResponseStatusCode;
            
            // Handle 404 response which might indicate channel issues
            if (responseCode == 404) {
                _logger?.LogError($"Channel not found (404): The channel may have expired (idle for 24h) or the limit of 20 channels was exceeded. Channel: {SocketChannel}");
                
                // Attempt to reconnect based on the thread's purpose
                switch (ThreadName.Split('-')[0]) {
                    case "userActivity":
                        _logger?.LogInformation("Attempting to recreate userActivity channel");
                        StartUserActivity();
                        break;
                    case "userAdherence":
                        _logger?.LogInformation("Attempting to recreate userAdherence channel");
                        StartUserAdherence();
                        break;
                    case "userCallStats":
                        _logger?.LogInformation("Attempting to recreate userCallStats channel");
                        StartUserCalls();
                        break;
                    case "userCallDets":
                        _logger?.LogInformation("Attempting to recreate userCallDets channel");
                        StartUserCallDets();
                        break;
                    case "queueCallDets":
                        _logger?.LogInformation("Attempting to recreate queueCallDets channel");
                        StartQueueCallDets();
                        break;
                }
                return;
            }
            
            success = ChilWebSocket.ValidateServerHandshake();
            if (success != true)
            {
                _logger.LogError(ChilWebSocket.LastErrorText);
                Console.WriteLine(ResponseBody);
                Console.WriteLine(ChilRest.ResponseHeader);
                return;
            }

            Console.WriteLine("Starting Receive");
            int consecutiveErrors = 0;
            
            while (!ShouldExit)
            {
                try {
                    Boolean SuccessFul = ChilWebSocket.ReadFrame();
                    
                    if (SuccessFul == true)
                    {
                        if (ChilWebSocket.FrameOpcodeInt == 1)
                        {
                            consecutiveErrors = 0;
                            TotalErrors = 0;
                            string ReceivedJson = ChilWebSocket.GetFrameData();
                            ReceiveData(ReceivedJson, ThreadName);
                            
                            // If ReceiveData set ShouldExit, but we need to reconnect, do it here
                            if (ShouldExit) {
                                _logger?.LogInformation($"Thread {ThreadName} is exiting due to reconnect request");
                                break;
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No Data");
                        Interlocked.Add(ref TotalErrors, 1);
                        consecutiveErrors++;

                        if (TotalErrors > 100)
                        {
                            _logger.LogError(ChilWebSocket.LastErrorText);
                            CleanupWebSocketResources(ChilWebSocket, ChilRest, SocketChannel, pingTimer);
                            
                            throw new ApplicationException(
                                $"Ending The Real Time Adapter Thread '{ThreadName}'. Error Count Exceeds Allowed"
                            );
                        }
                        
                        // Try to send a manual health check ping to see if connection is alive
                        if (SendWebSocketPing(ChilWebSocket) == false && consecutiveErrors > 5)
                        {
                            _logger?.LogWarning($"Multiple errors detected in WebSocket for {ThreadName}, attempting to recreate connection");
                            
                            // Try to force reconnection instead of breaking
                            ReceiveData("{\"topicName\": \"v2.system.socket_closing\", \"eventBody\": {\"message\": \"WebSocket Error\"}}", ThreadName);
                            
                            // If the reconnection was successful and handled in-place, continue
                            if (!ShouldExit) {
                                consecutiveErrors = 0;
                                continue;
                            }
                            
                            // Otherwise break the loop to let thread exit
                            break;
                        }
                        
                        Thread.Sleep(3000); // Prevent tight loop in case of errors
                        
                        ReceiveData("{\"topicName\": \"channel.metadata\", \"eventBody\": {\"message\": \"WebSocket Error\"}}", ThreadName);
                    }
                }
                catch (ThreadAbortException ex)
                {
                    _logger.LogError("Thread was aborted: " + ex.Message);
                    break;
                }
                catch (ThreadInterruptedException ex)
                {
                    _logger.LogError("Thread was interrupted: " + ex.Message);
                    break;
                }
                catch (Exception ex) 
                {
                    _logger?.LogError(ex, $"Exception in WebSocket read loop for {ThreadName}");
                    consecutiveErrors++;
                    
                    if (consecutiveErrors > 10) {
                        _logger?.LogWarning($"Too many consecutive errors in WebSocket for {ThreadName}, breaking connection loop");
                        break;
                    }
                    
                    Thread.Sleep(1000);
                }
            }
            
            CleanupWebSocketResources(ChilWebSocket, ChilRest, SocketChannel, pingTimer);
            _logger.LogWarning($"Thread '{ThreadName}' exiting because a shutdown was requested.");
        }
        
        private void CleanupWebSocketResources(Chilkat.WebSocket webSocket, Chilkat.Rest rest, string socketChannel, System.Timers.Timer pingTimer)
        {
            try {
                pingTimer.Enabled = false;
                pingTimer.Dispose();
                
                // Try to unsubscribe if possible
                string socketId = string.Empty;
                try {
                    Uri uri = new Uri(socketChannel);
                    string[] segments = uri.AbsolutePath.Split('/');
                    int channelIndex = Array.IndexOf(segments, "channels") + 1;
                    socketId = (channelIndex >= 0 && channelIndex < segments.Length) ? segments[channelIndex] : string.Empty;
                    
                    if (!string.IsNullOrEmpty(socketId)) {
                        string response = rest.FullRequestNoBody("DELETE", $"/api/v2/notifications/channels/{socketId}/subscriptions");
                        _logger?.LogInformation($"Deleted subscriptions from channel {socketId}");
                    }
                }
                catch (Exception ex) {
                    _logger?.LogWarning(ex, $"Failed to delete subscriptions from channel {socketId}");
                }
            }
            catch (Exception ex) {
                _logger?.LogError(ex, "Error during WebSocket cleanup");
            }
        }
        
        private bool SendWebSocketPing(Chilkat.WebSocket webSocket)
        {
            try {
                // Send ping according to Genesys docs
                string pingMessage = "{\"message\": \"ping\"}";
                bool success = webSocket.SendString(pingMessage);
                
                if (!success) {
                    _logger?.LogWarning("Failed to send WebSocket ping");
                    return false;
                }
                
                // Note: The response will come asynchronously and be handled 
                // in the normal ReadFrame/ReceiveData flow
                return true;
            }
            catch (Exception ex) {
                _logger?.LogError(ex, "Error sending WebSocket ping");
                return false;
            }
        }

        private void ReceiveData(String JsonString, string ThreadName)
        {
            if (JsonString.Contains("topicName"))
            {
                if (JsonString.IndexOf("v2.users") > 0 && JsonString.IndexOf("activity") > 0 && JsonString.IndexOf("systemPresence") > 0)
                {
                    TransActivity(JsonString);
                }
                else if (JsonString.IndexOf("v2.users") > 0 && JsonString.IndexOf("adherence") > 0)
                {
                    TransAdherence(JsonString);
                }
                else if (JsonString.IndexOf("v2.users") > 0 && JsonString.IndexOf("conversationsummary") > 0)
                {
                    TransCalls(JsonString);
                }
                else if (JsonString.IndexOf("v2.users") > 0 && JsonString.IndexOf("conversations") > 0)
                {
                    TransUserCallDets(JsonString);
                }
                else if (JsonString.IndexOf("routing.queues") > 0 && JsonString.IndexOf("conversations") > 0)
                {
                    TransQConv(JsonString);
                    if (WriteQueueDataCallsDets == true && DTQueueCallsDets.Rows.Count > 0)
                    {
                        DBAdapter.WriteSQLDataBulk(DTQueueCallsDets);
                        WriteQueueDataCallsDets = false;
                    }
                }
            }

            if (JsonString.IndexOf("WebSocket Heartbeat") > 0 || 
                (JsonString.Contains("\"topicName\": \"channel.metadata\"") && 
                 JsonString.Contains("\"message\": \"pong\"")))
            {
                Console.Write("==============================================================================================\n{0} Heartbeat:{1}\n" +
                              "==============================================================================================\n", ThreadName, DateTime.Now);

                // Reset error counters on successful heartbeat
                TotalErrors = 0;

                if ((DateTime.Now - LastChannelUpd).TotalSeconds > 21600)
                {
                    Console.WriteLine("Channel Update Being A Bit Proactive");
                    if (ChilKatJsonObj.GetAuthAPIKey() == true)
                    {
                        APIKey = ChilKatJsonObj.APIKey;
                        LastChannelUpd = DateTime.Now;
                    }
                }
            }
            else if (JsonString.IndexOf("WebSocket Error") > 0)
            {
                TotalErrors++;
                Console.Write("Error Counter:{0}\n", TotalErrors);
            }
            else if(JsonString.IndexOf("Websocket closing soon") > 0 ||
                    (JsonString.Contains("\"topicName\": \"v2.system.socket_closing\"")))
            {
                _logger?.LogWarning($"Received WebSocket closing notification for {ThreadName}, reconnecting");
                
                // Instead of starting a new thread and exiting this one, we'll recreate the connection in this thread
                try {
                    // Clear the socket to force reconnection but don't mark thread for exit
                    switch (ThreadName.Split('-')[0])
                    {
                        case "userActivity":
                            // Find our WebSocketDetail in the list and remove/recreate it
                            if (ThreadName.Contains("-")) {
                                int chunkIndex = int.Parse(ThreadName.Split('-')[1]);
                                if (chunkIndex > 0 && chunkIndex <= GCWebSocketActList.Count) {
                                    WebSocketDetail oldSocket = GCWebSocketActList[chunkIndex - 1];
                                    WebSocketDetail newSocket = CreateChannel(ThreadName);
                                    GCWebSocketActList[chunkIndex - 1] = newSocket;
                                    
                                    // Get the original chunk of users for this thread
                                    var chunks = ChunkDataTable(DTUserDetails, MAX_TOPICS_PER_SUBSCRIPTION);
                                    if (chunkIndex <= chunks.Count) {
                                        CreateUserActivitySubsForChunk(newSocket, chunks[chunkIndex - 1]);
                                        
                                        // Return to continue using this thread with the new connection
                                        return;
                                    }
                                }
                            } else {
                                GCWebSocketAct = CreateChannel("userActivity");
                                CreateUserActivitySubs(GCWebSocketAct);
                                
                                // Return to continue using this thread with the new connection
                                return;
                            }
                            break;
                            
                        case "userAdherence":
                            // Similar pattern for adherence
                            if (ThreadName.Contains("-")) {
                                int chunkIndex = int.Parse(ThreadName.Split('-')[1]);
                                if (chunkIndex > 0 && chunkIndex <= GCWebSocketAdhList.Count) {
                                    WebSocketDetail oldSocket = GCWebSocketAdhList[chunkIndex - 1];
                                    WebSocketDetail newSocket = CreateChannel(ThreadName);
                                    GCWebSocketAdhList[chunkIndex - 1] = newSocket;
                                    
                                    var chunks = ChunkDataTable(DTUserDetails, MAX_TOPICS_PER_SUBSCRIPTION);
                                    if (chunkIndex <= chunks.Count) {
                                        CreateUserAdherenceSubsForChunk(newSocket, chunks[chunkIndex - 1]);
                                        return;
                                    }
                                }
                            } else {
                                GCWebSocketAdh = CreateChannel("userAdherence");
                                CreateUserAdherenceSubs(GCWebSocketAdh);
                                return;
                            }
                            break;
                            
                        case "userCallStats":
                            // Similar pattern for call stats
                            if (ThreadName.Contains("-")) {
                                int chunkIndex = int.Parse(ThreadName.Split('-')[1]);
                                if (chunkIndex > 0 && chunkIndex <= GCWebSocketCallsList.Count) {
                                    WebSocketDetail oldSocket = GCWebSocketCallsList[chunkIndex - 1];
                                    WebSocketDetail newSocket = CreateChannel(ThreadName);
                                    GCWebSocketCallsList[chunkIndex - 1] = newSocket;
                                    
                                    var chunks = ChunkDataTable(DTUserDetails, MAX_TOPICS_PER_SUBSCRIPTION);
                                    if (chunkIndex <= chunks.Count) {
                                        CreateUserCallSubsForChunk(newSocket, chunks[chunkIndex - 1]);
                                        return;
                                    }
                                }
                            } else {
                                GCWebSocketCalls = CreateChannel("userCallStats");
                                CreateUserCallSubs(GCWebSocketCalls);
                                return;
                            }
                            break;
                            
                        case "userCallDets":
                            // Similar pattern for call details
                            if (ThreadName.Contains("-")) {
                                int chunkIndex = int.Parse(ThreadName.Split('-')[1]);
                                if (chunkIndex > 0 && chunkIndex <= GCWebSocketCallDetsList.Count) {
                                    WebSocketDetail oldSocket = GCWebSocketCallDetsList[chunkIndex - 1];
                                    WebSocketDetail newSocket = CreateChannel(ThreadName);
                                    GCWebSocketCallDetsList[chunkIndex - 1] = newSocket;
                                    
                                    var chunks = ChunkDataTable(DTUserDetails, MAX_TOPICS_PER_SUBSCRIPTION);
                                    if (chunkIndex <= chunks.Count) {
                                        CreateUserCallDetSubsForChunk(newSocket, chunks[chunkIndex - 1]);
                                        return;
                                    }
                                }
                            } else {
                                GCWebSocketCallDets = CreateChannel("userCallDets");
                                CreateUserCallDetSubs(GCWebSocketCallDets);
                                return;
                            }
                            break;
                            
                        case "queueCallDets":
                            // Similar pattern for queue call details
                            if (ThreadName.Contains("-")) {
                                int chunkIndex = int.Parse(ThreadName.Split('-')[1]);
                                if (chunkIndex > 0 && chunkIndex <= GCWebSocketQueueCallDetsList.Count) {
                                    WebSocketDetail oldSocket = GCWebSocketQueueCallDetsList[chunkIndex - 1];
                                    WebSocketDetail newSocket = CreateChannel(ThreadName);
                                    GCWebSocketQueueCallDetsList[chunkIndex - 1] = newSocket;
                                    
                                    var chunks = ChunkDataTable(DTQueueDetails, MAX_TOPICS_PER_SUBSCRIPTION);
                                    if (chunkIndex <= chunks.Count) {
                                        CreateQueueCallDetSubsForChunk(newSocket, chunks[chunkIndex - 1]);
                                        return;
                                    }
                                }
                            } else {
                                GCWebSocketQueueCallDets = CreateChannel("queueCallDets");
                                CreateQueueCallDetSubs(GCWebSocketQueueCallDets);
                                return;
                            }
                            break;
                    }
                    
                    // Only if we couldn't handle the reconnection in-place, start a new connection in a new thread
                    _logger?.LogWarning($"Could not reconnect {ThreadName} in-place, starting new connection thread");
                    switch (ThreadName.Split('-')[0])
                    {
                        case "userActivity":
                            StartUserActivity();
                            break;
                        case "userAdherence":
                            StartUserAdherence();
                            break;
                        case "userCallStats":
                            StartUserCalls();
                            break;
                        case "userCallDets":
                            StartUserCallDets();
                            break;
                        case "queueCallDets":
                            StartQueueCallDets();
                            break;
                    }
                }
                catch (Exception ex) {
                    _logger?.LogError(ex, $"Error during WebSocket reconnection for {ThreadName}");
                }
                
                // Only set ShouldExit if we attempted to create a new connection
                ShouldExit = true;
            }
            else
            {
                if (WriteUserDataAct == true && DTUserData.Rows.Count > 0)
                {
                    DBAdapter.WriteSQLDataBulk(DTUserData);
                    WriteUserDataAct = false;
                }
                else if (WriteUserDataAdh == true && DTUserData.Rows.Count > 0)
                {
                    DBAdapter.WriteSQLDataBulk(DTUserData);
                    WriteUserDataAdh = false;
                }
                else if (WriteUserDataCalls == true && DTUserData.Rows.Count > 0)
                {
                    DBAdapter.WriteSQLDataBulk(DTUserData);
                    WriteUserDataCalls = false;
                }
                else if (WriteUserDataCallsDets == true && DTUserCallsDets.Rows.Count > 0)
                {
                    DBAdapter.WriteSQLDataBulk(DTUserCallsDets);
                    WriteUserDataCallsDets = false;
                }
            }
        }

        public void StartUserRealTime()
        {
            StartUserActivity();
            StartUserAdherence();
            StartUserCalls();
            StartUserCallDets();
            StartQueueCallDets();
        }

        // Create a method to chunk data rows for subscription
        private List<DataTable> ChunkDataTable(DataTable source, int chunkSize)
        {
            List<DataTable> chunks = new List<DataTable>();
            int rowCount = source.Rows.Count;
            int chunkCount = (int)Math.Ceiling((double)rowCount / chunkSize);
            
            for (int i = 0; i < chunkCount; i++)
            {
                DataTable chunk = source.Clone();
                int startIdx = i * chunkSize;
                int endIdx = Math.Min(startIdx + chunkSize, rowCount);
                
                for (int j = startIdx; j < endIdx; j++)
                {
                    chunk.ImportRow(source.Rows[j]);
                }
                
                chunks.Add(chunk);
            }
            return chunks;
        }

        public void StartUserActivity()
        {
            String SyncName = "userActivity";
            DataTable DTUserActivity = CreateActivityTable();

            // Clear existing connections for this function
            GCWebSocketActList.Clear();

            // Check if we need to create multiple channels due to user count exceeding topic limit
            if (DTUserDetails.Rows.Count > MAX_TOPICS_PER_SUBSCRIPTION)
            {
                var chunks = ChunkDataTable(DTUserDetails, MAX_TOPICS_PER_SUBSCRIPTION);
                _logger?.LogInformation($"Splitting user activity into {chunks.Count} channels due to {DTUserDetails.Rows.Count} users");

                // Create a separate channel and WebSocket for each chunk of 1,000 users
                int chunkIndex = 1;
                foreach (var chunk in chunks)
                {
                    // Create a new channel for this chunk
                    WebSocketDetail socket = CreateChannel($"{SyncName}-{chunkIndex}");
                    GCWebSocketActList.Add(socket);
                    
                    // Create a subscription for this chunk on this channel
                    CreateUserActivitySubsForChunk(socket, chunk);
                    
                    // Start a WebSocket connection for this channel
                    Thread thread = new Thread(() => CreateWebSocket(socket.connectUri, socket.id, $"{SyncName}-{chunkIndex}"));
                    thread.Name = $"{SyncName}-{chunkIndex}";
                    thread.Start();
                    WebSocketThreads.Add(thread);
                    
                    chunkIndex++;
                }
            }
            else
            {
                // For fewer than 1,000 users, just use a single channel
                GCWebSocketAct = CreateChannel(SyncName);
                GCWebSocketActList.Add(GCWebSocketAct);
                CreateUserActivitySubs(GCWebSocketAct);
                
                Thread thread = new Thread(() => CreateWebSocket(GCWebSocketAct.connectUri, GCWebSocketAct.id, SyncName));
                thread.Name = SyncName;
                thread.Start();
                WebSocketThreads.Add(thread);
            }
        }

        public void StartUserAdherence()
        {
            String SyncName = "userAdherence";
            DataTable DTUserAdherence = CreateAdherenceTable();
            
            // Clear existing connections for this function
            GCWebSocketAdhList.Clear();

            // Check if we need to create multiple channels due to user count exceeding topic limit
            if (DTUserDetails.Rows.Count > MAX_TOPICS_PER_SUBSCRIPTION)
            {
                var chunks = ChunkDataTable(DTUserDetails, MAX_TOPICS_PER_SUBSCRIPTION);
                _logger?.LogInformation($"Splitting user adherence into {chunks.Count} channels due to {DTUserDetails.Rows.Count} users");

                // Create a separate channel and WebSocket for each chunk of 1,000 users
                int chunkIndex = 1;
                foreach (var chunk in chunks)
                {
                    // Create a new channel for this chunk
                    WebSocketDetail socket = CreateChannel($"{SyncName}-{chunkIndex}");
                    GCWebSocketAdhList.Add(socket);
                    
                    // Create a subscription for this chunk on this channel
                    CreateUserAdherenceSubsForChunk(socket, chunk);
                    
                    // Start a WebSocket connection for this channel
                    Thread thread = new Thread(() => CreateWebSocket(socket.connectUri, socket.id, $"{SyncName}-{chunkIndex}"));
                    thread.Name = $"{SyncName}-{chunkIndex}";
                    thread.Start();
                    WebSocketThreads.Add(thread);
                    
                    chunkIndex++;
                }
            }
            else
            {
                // For fewer than 1,000 users, just use a single channel
                GCWebSocketAdh = CreateChannel(SyncName);
                GCWebSocketAdhList.Add(GCWebSocketAdh);
                CreateUserAdherenceSubs(GCWebSocketAdh);
                
                Thread thread = new Thread(() => CreateWebSocket(GCWebSocketAdh.connectUri, GCWebSocketAdh.id, SyncName));
                thread.Name = SyncName;
                thread.Start();
                WebSocketThreads.Add(thread);
            }
        }

        public void StartUserCalls()
        {
            String SyncName = "userCallStats";
            DataTable DTUserCallStats = CreateCallStatsTable();
            
            // Clear existing connections for this function
            GCWebSocketCallsList.Clear();

            // Check if we need to create multiple channels due to user count exceeding topic limit
            if (DTUserDetails.Rows.Count > MAX_TOPICS_PER_SUBSCRIPTION)
            {
                var chunks = ChunkDataTable(DTUserDetails, MAX_TOPICS_PER_SUBSCRIPTION);
                _logger?.LogInformation($"Splitting user call stats into {chunks.Count} channels due to {DTUserDetails.Rows.Count} users");

                // Create a separate channel and WebSocket for each chunk of 1,000 users
                int chunkIndex = 1;
                foreach (var chunk in chunks)
                {
                    // Create a new channel for this chunk
                    WebSocketDetail socket = CreateChannel($"{SyncName}-{chunkIndex}");
                    GCWebSocketCallsList.Add(socket);
                    
                    // Create a subscription for this chunk on this channel
                    CreateUserCallSubsForChunk(socket, chunk);
                    
                    // Start a WebSocket connection for this channel
                    Thread thread = new Thread(() => CreateWebSocket(socket.connectUri, socket.id, $"{SyncName}-{chunkIndex}"));
                    thread.Name = $"{SyncName}-{chunkIndex}";
                    thread.Start();
                    WebSocketThreads.Add(thread);
                    
                    chunkIndex++;
                }
            }
            else
            {
                // For fewer than 1,000 users, just use a single channel
                GCWebSocketCalls = CreateChannel(SyncName);
                GCWebSocketCallsList.Add(GCWebSocketCalls);
                CreateUserCallSubs(GCWebSocketCalls);
                
                Thread thread = new Thread(() => CreateWebSocket(GCWebSocketCalls.connectUri, GCWebSocketCalls.id, SyncName));
                thread.Name = SyncName;
                thread.Start();
                WebSocketThreads.Add(thread);
            }
        }

        public void StartUserCallDets()
        {
            String SyncName = "userCallDets";
            DataTable DTUserCallStatsDets = CreateCallDetsTable();
            
            // Clear existing connections for this function
            GCWebSocketCallDetsList.Clear();

            // Check if we need to create multiple channels due to user count exceeding topic limit
            if (DTUserDetails.Rows.Count > MAX_TOPICS_PER_SUBSCRIPTION)
            {
                var chunks = ChunkDataTable(DTUserDetails, MAX_TOPICS_PER_SUBSCRIPTION);
                _logger?.LogInformation($"Splitting user call details into {chunks.Count} channels due to {DTUserDetails.Rows.Count} users");

                // Create a separate channel and WebSocket for each chunk of 1,000 users
                int chunkIndex = 1;
                foreach (var chunk in chunks)
                {
                    // Create a new channel for this chunk
                    WebSocketDetail socket = CreateChannel($"{SyncName}-{chunkIndex}");
                    GCWebSocketCallDetsList.Add(socket);
                    
                    // Create a subscription for this chunk on this channel
                    CreateUserCallDetSubsForChunk(socket, chunk);
                    
                    // Start a WebSocket connection for this channel
                    Thread thread = new Thread(() => CreateWebSocket(socket.connectUri, socket.id, $"{SyncName}-{chunkIndex}"));
                    thread.Name = $"{SyncName}-{chunkIndex}";
                    thread.Start();
                    WebSocketThreads.Add(thread);
                    
                    chunkIndex++;
                }
            }
            else
            {
                // For fewer than 1,000 users, just use a single channel
                GCWebSocketCallDets = CreateChannel(SyncName);
                GCWebSocketCallDetsList.Add(GCWebSocketCallDets);
                CreateUserCallDetSubs(GCWebSocketCallDets);
                
                Thread thread = new Thread(() => CreateWebSocket(GCWebSocketCallDets.connectUri, GCWebSocketCallDets.id, SyncName));
                thread.Name = SyncName;
                thread.Start();
                WebSocketThreads.Add(thread);
            }
        }

        public void StartQueueCallDets()
        {
            String SyncName = "queueCallDets";
            DataTable DTUserCallStatsDets = CreateQueueCallDetsTable();

            QueueObsRealTime QueueInit = new QueueObsRealTime();
            QueueInit.Initialize();
            QueueInit.DTQueueDetails = DTQueueDetails;
            QueueInit.getQueueStatus();

            DataTable DTTempQueue = DBAdapter.CreateInMemTable("queuerealtimeconvdata");
            DataTable DTTempUser = DBAdapter.CreateInMemTable("userrealtimeconvdata");

            if (QueueInit.DTQueueConvActive != null && QueueInit.DTQueueConvActive.Rows.Count > 0)
            {
                foreach (DataRow DrConv in QueueInit.DTQueueConvActive.Rows)
                {
                    DTTempQueue.ImportRow(DrConv);

                    if (DrConv["actingas"].ToString() == "agent")
                        DTTempUser.ImportRow(DrConv);
                }

                Console.WriteLine("Rows From Obs {0}", DTUserCallStatsDets.Rows.Count);

                if (DTTempQueue != null && DTTempQueue.Rows.Count > 0)
                {
                    DBAdapter.WriteSQLDataSync(DTTempQueue, "queueRealTimeConvData");
                    DTQueueCallsDets = DTTempQueue.Copy();
                }

                if (DTTempUser != null && DTTempUser.Rows.Count > 0)
                {
                    DBAdapter.WriteSQLDataBulk(DTTempUser);
                    DTUserCallsDets = DTTempUser.Copy();
                }

                DTTempQueue.Dispose();
                DTTempUser.Dispose();
            }

            // Clear existing connections for this function
            GCWebSocketQueueCallDetsList.Clear();
            
            // Check if we need to create multiple channels due to queue count exceeding topic limit
            if (DTQueueDetails.Rows.Count > MAX_TOPICS_PER_SUBSCRIPTION)
            {
                var chunks = ChunkDataTable(DTQueueDetails, MAX_TOPICS_PER_SUBSCRIPTION);
                _logger?.LogInformation($"Splitting queue call details into {chunks.Count} channels due to {DTQueueDetails.Rows.Count} queues");

                // Create a separate channel and WebSocket for each chunk of 1,000 queues
                int chunkIndex = 1;
                foreach (var chunk in chunks)
                {
                    // Create a new channel for this chunk
                    WebSocketDetail socket = CreateChannel($"{SyncName}-{chunkIndex}");
                    GCWebSocketQueueCallDetsList.Add(socket);
                    
                    // Create a subscription for this chunk on this channel
                    CreateQueueCallDetSubsForChunk(socket, chunk);
                    
                    // Start a WebSocket connection for this channel
                    Thread thread = new Thread(() => CreateWebSocket(socket.connectUri, socket.id, $"{SyncName}-{chunkIndex}"));
                    thread.Name = $"{SyncName}-{chunkIndex}";
                    thread.Start();
                    WebSocketThreads.Add(thread);
                    
                    chunkIndex++;
                }
            }
            else
            {
                // For fewer than 1,000 queues, just use a single channel
                GCWebSocketQueueCallDets = CreateChannel(SyncName);
                GCWebSocketQueueCallDetsList.Add(GCWebSocketQueueCallDets);
                CreateQueueCallDetSubs(GCWebSocketQueueCallDets);
                
                Thread thread = new Thread(() => CreateWebSocket(GCWebSocketQueueCallDets.connectUri, GCWebSocketQueueCallDets.id, SyncName));
                thread.Name = SyncName;
                thread.Start();
                WebSocketThreads.Add(thread);
            }
        }

        private void CreateWebSocket(WebSocketDetail GCWebSocket)
        {
            int retryCount = 0;
            const int maxRetries = 3;
            while (retryCount < maxRetries)
            {
                try
                {
                    Console.WriteLine("\nCreating Channel For: {0} (Attempt {1})", GCWebSocket.ReportName, retryCount + 1);
                    CreateWebSocket(APISockURL, GCWebSocket.connectUri, GCWebSocket.ReportName);
                    break;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    _logger?.LogError(ex, $"Error creating WebSocket for {GCWebSocket.ReportName}, attempt {retryCount} of {maxRetries}");
                    
                    if (retryCount >= maxRetries)
                    {
                        _logger?.LogError($"Failed to create WebSocket for {GCWebSocket.ReportName} after {maxRetries} attempts");
                        throw;
                    }
                    
                    // Exponential backoff
                    Thread.Sleep(TimeSpan.FromSeconds(Math.Pow(2, retryCount)));
                }
            }
        }

        private DataTable GetUsers()
        {
            DataTable Users = DBAdapter.GetSQLTableData("select id,name from userDetails where state = 'active'", "UserDetails");
            return Users;
        }

        private DataTable GetQueues()
        {
            DataTable Queues = DBAdapter.GetSQLTableData("select id,name from QueueDetails", "QueueDetails");
            return Queues;
        }

        private DataTable CreateAdherenceTable()
        {
            DataTable DTTemp = new DataTable();
            DTTemp.TableName = "userAdherenceData";
            DTTemp.Columns.Add("id", typeof(String));
            DTTemp.Columns.Add("adherenceState", typeof(String));
            DTTemp.Columns.Add("adherenceChangeTime", typeof(DateTime));
            DTTemp.Columns.Add("impact", typeof(String));
            DTTemp.Columns.Add("scheduledActivityCategory", typeof(String));
            return DTTemp;
        }

        private DataTable CreateCallDetsTable()
        {
            DataTable DTTemp = new DataTable();
            DTTemp.TableName = "userCallDets";
            DTTemp.Columns.Add("id", typeof(String));
            DTTemp.Columns.Add("conversationid", typeof(String));
            return DTTemp;
        }

        private DataTable CreateQueueCallDetsTable()
        {
            DataTable DTTemp = new DataTable();
            DTTemp.TableName = "queueCallDets";
            DTTemp.Columns.Add("id", typeof(String));
            DTTemp.Columns.Add("conversationid", typeof(String));
            return DTTemp;
        }

        private DataTable CreateActivityTable()
        {
            DataTable DTTemp = new DataTable();
            DTTemp.TableName = "userActivityData";
            DTTemp.Columns.Add("id", typeof(String));
            DTTemp.Columns.Add("routingStatus", typeof(String));
            DTTemp.Columns.Add("routingDate", typeof(DateTime));
            DTTemp.Columns.Add("systemPresence", typeof(String));
            DTTemp.Columns.Add("presenceId", typeof(String));
            DTTemp.Columns.Add("presenceDate", typeof(DateTime));
            return DTTemp;
        }

        private DataTable CreateCallStatsTable()
        {
            DataTable DTTemp = new DataTable();
            DTTemp.TableName = "userCallStatData";
            DTTemp.Columns.Add("id", typeof(String));
            DTTemp.Columns.Add("cccallactive", typeof(int));
            DTTemp.Columns.Add("cccallacw", typeof(int));
            DTTemp.Columns.Add("othcallactive", typeof(int));
            DTTemp.Columns.Add("othcallacw", typeof(int));
            DTTemp.Columns.Add("cbcallactive", typeof(int));
            DTTemp.Columns.Add("cbcallacw", typeof(int));
            DTTemp.Columns.Add("cbothcallactive", typeof(int));
            DTTemp.Columns.Add("cbothcallacw", typeof(int));
            DTTemp.Columns.Add("ccemailactive", typeof(int));
            DTTemp.Columns.Add("ccemailacw", typeof(int));
            DTTemp.Columns.Add("othemailactive", typeof(int));
            DTTemp.Columns.Add("othemailacw", typeof(int));
            DTTemp.Columns.Add("ccchatactive", typeof(int));
            DTTemp.Columns.Add("ccchatacw", typeof(int));
            DTTemp.Columns.Add("othchatactive", typeof(int));
            DTTemp.Columns.Add("othchatacw", typeof(int));
            return DTTemp;
        }

        private void ClearCacheTable(String RealTimeName)
        {
            int rowsAffected = DBAdapter.ExecuteSqlNonQuery("Delete from " + RealTimeName);
            Console.WriteLine("\nCleared Cache Table - {1} rows affected: {0}", rowsAffected, RealTimeName);
        }

        private WebSocketDetail CreateChannel(string ReportName)
        {
            WebSocketDetail WSSocket = new WebSocketDetail();

            string JsonString = ChilKatJsonObj.ReturnJson("/api/v2/notifications/channels", "");

            WSSocket = JsonConvert.DeserializeObject<WebSocketDetail>(JsonString,
                   new JsonSerializerSettings
                   {
                       NullValueHandling = NullValueHandling.Ignore
                   });

            WSSocket.ReportName = ReportName;

            return WSSocket;
        }

        private void CreateQueueCallDetSubs(WebSocketDetail WebSock)
        {
            Console.WriteLine("Creating Call Summary Channel For Queues");

            //Create Subscriptions for Notification - Calls.

            StringBuilder SubscriptionJSON = new StringBuilder();
            int Counter = 0;
            foreach (DataRow DRRow in DTQueueDetails.Rows)
            {
                SubscriptionJSON.Append(" \n{ \"id\": \"v2.routing.queues." + DRRow["id"].ToString() + ".conversations\"},");
                ++Counter;
                if (Counter > 999)
                    break;
            }

            if (Counter > 0)
            {
                SubscriptionJSON.Length = SubscriptionJSON.Length - 1;
                string JSONBodyString = "[" + SubscriptionJSON.ToString() + " ]";
                string URL = "/api/v2/notifications/channels/" + WebSock.id + "/subscriptions";
                //Console.WriteLine("Activity Subscription for the following agents: {0}", SubscriptionJSON.ToString());

                Console.WriteLine("API Key: {1} Acti Sock ID: {0} ", WebSock.id, APIKey.Substring(0, 6));

                string JsonString = ChilKatJsonObj.ReturnJson(URL, JSONBodyString);
                //Console.WriteLine(JSONBodyString);
            }
        }

        private void CreateUserCallDetSubs(WebSocketDetail WebSock)
        {
            Console.WriteLine("Creating Call Summary Channel For Users");

            //Create Subscriptions for Notification - Calls.

            StringBuilder SubscriptionJSON = new StringBuilder();
            int Counter = 0;
            foreach (DataRow DRRow in DTUserDetails.Rows)
            {
                SubscriptionJSON.Append(" \n{ \"id\": \"v2.users." + DRRow["id"].ToString() + ".conversations\"},");
                ++Counter;
            }

            if (Counter > 0)
            {
                SubscriptionJSON.Length = SubscriptionJSON.Length - 1;
                string JSONBodyString = "[" + SubscriptionJSON.ToString() + " ]";
                string URL = "/api/v2/notifications/channels/" + WebSock.id + "/subscriptions";
                //Console.WriteLine("Call Dets Subscription for the following agents: {0}", SubscriptionJSON.ToString());

                Console.WriteLine("API Key: {1} Acti Sock ID: {0} ", WebSock.id, APIKey.Substring(0, 6));

                string JsonString = ChilKatJsonObj.ReturnJson(URL, JSONBodyString);
                //Console.WriteLine(JSONBodyString);
            }
        }

        private void CreateUserCallSubs(WebSocketDetail WebSock)
        {
            Console.WriteLine("Creating Call Summary Channel For Users");
            //Console.WriteLine("Call Dets Subscription for the following agents: {0}", SubscriptionJSON.ToString());
            //Create Subscriptions for Notification - Calls.

            StringBuilder SubscriptionJSON = new StringBuilder();
            int Counter = 0;
            foreach (DataRow DRRow in DTUserDetails.Rows)
            {
                SubscriptionJSON.Append(" \n{ \"id\": \"v2.users." + DRRow["id"].ToString() + ".conversationsummary\"},");
                ++Counter;
            }

            if (Counter > 0)
            {
                SubscriptionJSON.Length = SubscriptionJSON.Length - 1;
                string JSONBodyString = "[" + SubscriptionJSON.ToString() + " ]";
                string URL = "/api/v2/notifications/channels/" + WebSock.id + "/subscriptions";

                Console.WriteLine("API Key: {1} Acti Sock ID: {0} ", WebSock.id, APIKey.Substring(0, 6));

                string JsonString = ChilKatJsonObj.ReturnJson(URL, JSONBodyString);

                //Console.WriteLine(JSONBodyString);
            }
        }

        private void CreateUserActivitySubs(WebSocketDetail WebSock)
        {
            Console.WriteLine("Creating Activity Channel For Users");

            //Create Subscriptions for Notification - Activity and WFM.

            StringBuilder SubscriptionJSON = new StringBuilder();
            int Counter = 0;
            Console.WriteLine("User Details has {0} Rows", DTUserDetails.Rows.Count);
            foreach (DataRow DRRow in DTUserDetails.Rows)
            {
                SubscriptionJSON.Append(" \n{ \"id\": \"v2.users." + DRRow["id"].ToString() + ".activity\"},");
                ++Counter;
            }

            //Console.WriteLine("Activity Subscription for the following agents: {0}", SubscriptionJSON.ToString());

            if (Counter > 0)
            {
                SubscriptionJSON.Length = SubscriptionJSON.Length - 1;
                string JSONBodyString = "[" + SubscriptionJSON.ToString() + " ]";
                string URL = "/api/v2/notifications/channels/" + WebSock.id + "/subscriptions";

                Console.WriteLine("API Key: {1} Acti Sock ID: {0} ", WebSock.id, APIKey.Substring(0, 6));

                string JsonString = ChilKatJsonObj.ReturnJson(URL, JSONBodyString);
                //Console.WriteLine(JSONBodyString);
            }
        }

        private void CreateUserAdherenceSubs(WebSocketDetail WebSock)
        {
            Console.WriteLine("Creating Adherence Channel For Users");

            //Create Subscriptions for Notification - Activity and WFM.

            StringBuilder SubscriptionJSON = new StringBuilder();
            int Counter = 0;
            foreach (DataRow DRRow in DTUserDetails.Rows)
            {
                SubscriptionJSON.Append(" \n{ \"id\": \"v2.users." + DRRow["id"].ToString() + ".workforcemanagement.adherence\"},");
                ++Counter;
            }

            //Console.WriteLine("Adherence Subscription for the following agents: {0}", SubscriptionJSON.ToString());

            if (Counter > 0)
            {
                SubscriptionJSON.Length = SubscriptionJSON.Length - 1;
                string JSONBodyString = "[" + SubscriptionJSON.ToString() + " ]";
                string URL = "/api/v2/notifications/channels/" + WebSock.id + "/subscriptions";

                Console.WriteLine("API Key: {1} Acti Sock ID: {0} ", WebSock.id, APIKey.Substring(0, 6));

                string JsonString = ChilKatJsonObj.ReturnJson(URL, JSONBodyString);
                //Console.WriteLine(JSONBodyString);
            }
        }

        private void TimerCallBack(object sender, ElapsedEventArgs elapsedEventArg)
        {
            try
            {
                if (ShouldExit)
                    return;

                Console.WriteLine("\nProcessing DB Check:{0}", SyncType);

                switch (SyncType)
                {
                    case "userActivity":
                    case "userCalls":
                    case "queueCalls":
                        UserUpdate();
                        break;
                    default:
                        //ConvUpdate();
                        break;
                }
            }
            catch (Exception ex)
            {
                // Exceptions in this callback (from System.Timers.ElapsedEventHandler) are ignored and not logged.
                // Legacy code did not handle this at all, and all exceptions were silently ignored.
                // May need to move out of the timer callback for error handling to work as expected.
                // TODO: Throw here when comfortable all common exceptions have been fixed/handled.
                _logger.LogError(ex, "Suppressed error");
            }
        }

        public DataTable GetUserStatus()
        {
            DataTable Users = DBAdapter.CreateInMemTable("userRealTimeData");
            //DataTable Users = CreateUsersTable();
            int CurrentPage = 1;
            int MaxPages = 30;
            int UserCounter = 0;

            while (CurrentPage <= MaxPages)
            {
                string JsonString = ChilKatJsonObj.ReturnJson("/api/v2/users?state=active&pageSize=500&pageNumber=" + CurrentPage + "&expand=presence%2CroutingStatus%2Cgeolocation%2CconversationSummary&sortOrder=asc");

                UserReal.UserRealTime UserData = new UserReal.UserRealTime();

                UserData = JsonConvert.DeserializeObject<UserReal.UserRealTime>(JsonString,
                       new JsonSerializerSettings
                       {
                           NullValueHandling = NullValueHandling.Ignore
                       });

                MaxPages = UserData.pageCount;

                foreach (UserReal.Entity JSON in UserData.entities)
                {
                    if (UserCounter % 100 == 0)
                        Console.Write("#");

                    DataRow UserRow = Users.Select("id='" + JSON.id + "'").FirstOrDefault();

                    if (UserRow != null)
                        UserRow.AcceptChanges();
                    else
                    {
                        UserRow = Users.NewRow();
                        Console.Write("+");
                    }

                    JSON.routingStatus.startTime = new DateTime(
                                      JSON.routingStatus.startTime.Ticks - (JSON.routingStatus.startTime.Ticks % TimeSpan.TicksPerSecond),
                                      JSON.routingStatus.startTime.Kind
                                    );
                    if (JSON.routingStatus.startTime.Year < 2000)
                        JSON.routingStatus.startTime = DateTime.Parse("2000-01-01");

                    if (UserRow.RowState == DataRowState.Detached || ((string)UserRow["routingstatus"] != JSON.routingStatus.status
                             || (string)UserRow["systempresence"] != JSON.presence.presenceDefinition.systemPresence
                             || (string)UserRow["presenceid"] != JSON.presence.presenceDefinition.id
                             || (DateTime)UserRow["routstarttime"] != JSON.routingStatus.startTime)
                       )
                    {
                        UserRow["id"] = JSON.id;
                        UserRow["name"] = JSON.name;
                        UserRow["email"] = JSON.email;
                        UserRow["jabberId"] = JSON.chat.jabberId;
                        UserRow["state"] = JSON.state;
                        UserRow["title"] = JSON.title;
                        UserRow["username"] = JSON.username;
                        UserRow["department"] = JSON.department;
                        UserRow["routingstatus"] = JSON.routingStatus.status;
                        UserRow["routstarttime"] = JSON.routingStatus.startTime;
                        if (JSON.presence != null)
                        {
                            UserRow["systempresence"] = JSON.presence.presenceDefinition.systemPresence;
                            UserRow["presenceid"] = JSON.presence.presenceDefinition.id;
                            UserRow["presstarttime"] = JSON.presence.modifiedDate;
                        }
                        UserRow["cccallactive"] = JSON.conversationSummary.call.contactCenter.active;
                        UserRow["cccallacw"] = JSON.conversationSummary.call.contactCenter.acw;
                        UserRow["othcallactive"] = JSON.conversationSummary.call.enterprise.active;
                        UserRow["cbcallactive"] = JSON.conversationSummary.callback.contactCenter.active;
                        UserRow["cbcallacw"] = JSON.conversationSummary.callback.contactCenter.acw;
                        UserRow["cbothcallactive"] = JSON.conversationSummary.callback.enterprise.active;
                        UserRow["cccallactive"] = JSON.conversationSummary.call.contactCenter.active;
                        UserRow["cccallacw"] = JSON.conversationSummary.call.contactCenter.acw;
                        UserRow["othcallactive"] = JSON.conversationSummary.call.enterprise.active;
                        UserRow["cbcallactive"] = JSON.conversationSummary.callback.contactCenter.active;
                        UserRow["cbcallacw"] = JSON.conversationSummary.callback.contactCenter.acw;
                        UserRow["cbothcallactive"] = JSON.conversationSummary.callback.enterprise.active;
                        UserRow["ccemailactive"] = JSON.conversationSummary.email.contactCenter.active;
                        UserRow["ccemailacw"] = JSON.conversationSummary.email.contactCenter.acw;
                        UserRow["othemailactive"] = JSON.conversationSummary.email.enterprise.active;
                        UserRow["ccchatactive"] = JSON.conversationSummary.chat.contactCenter.active;
                        UserRow["ccchatacw"] = JSON.conversationSummary.chat.contactCenter.acw;
                        UserRow["othchatactive"] = JSON.conversationSummary.chat.enterprise.active;
                    }
                    if (UserRow.RowState == DataRowState.Detached)
                    {
                        Users.Rows.Add(UserRow);
                    }
                    ++UserCounter;
                }
                CurrentPage++;
            }

            Console.WriteLine("\nWe Have Returned {0} Row(s)", UserCounter);
            return Users;
        }

        private Boolean TransActivity(string JsonString)
        {
            Boolean Successful = false;

            Console.Write("Act:");
            try
            {
                RealUA.Activity UserActivity = new RealUA.Activity();
                UserActivity = JsonConvert.DeserializeObject<RealUA.Activity>(JsonString,
                            new JsonSerializerSettings
                            {
                                NullValueHandling = NullValueHandling.Ignore
                            });
                RealUA.EventbodyUser RealUserInfo = UserActivity.eventBody;
                lock (DTUserData)
                {
                    DataRow DRUserAct = DTUserData.Select("id = '" + RealUserInfo.id + "'").FirstOrDefault();

                    if (DRUserAct != null)
                    {
                        Console.WriteLine("Act: Updating user data for ID: " + RealUserInfo.id);
                        DRUserAct["routingStatus"] = RealUserInfo.routingStatus.status;
                        DRUserAct["routstarttime"] = RealUserInfo.routingStatus.startTime;
                        DRUserAct["systemPresence"] = RealUserInfo.presence.presenceDefinition.systemPresence;
                        DRUserAct["presenceId"] = RealUserInfo.presence.presenceDefinition.id;
                        DRUserAct["presstarttime"] = RealUserInfo.presence.modifiedDate;
                    }
                    else
                    {
                        Console.WriteLine("Act: No matching user data found for ID: " + RealUserInfo.id);
                    }
                    WriteUserDataAct = true;
                    Successful = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Act: Error in TransActivity - " + ex.Message);
                Successful = false;
            }
            finally
            {
                Console.WriteLine("Act: Finished TransActivity with Successful status: " + Successful);
            }
            return Successful;
        }

        private Boolean TransUserCallDets(string JsonString)
        {
            TimeZoneInfo AppTimeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneConfig);

            Boolean Successful = false;
            Console.Write("CllDets:");
            lock (DTUserCallsDets)
            {
                RealCN.Conversations UserConvs = new RealCN.Conversations();
                UserConvs = JsonConvert.DeserializeObject<RealCN.Conversations>(JsonString,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    });

                RealCN.Eventbody RealConversation = UserConvs.eventBody;
                Console.Write("Conv={0}:", RealConversation.id.Substring(0, 5));

                foreach (RealCN.Participant ConvPart in RealConversation.participants)
                {
                    if (ConvPart.purpose == "agent" || ConvPart.purpose == "user")
                    {
                        bool RowFound = false;
                        DataRow ConvDetails = DTUserCallsDets.Select("conversationid = '" + RealConversation.id + "' and userid='" + ConvPart.userId + "'").FirstOrDefault();

                        if (ConvDetails == null)
                        {
                            ConvDetails = DTUserCallsDets.NewRow();
                        }
                        else
                        {
                            RowFound = true;
                        }

                        ConvDetails["keyid"] = ConvPart.userId + "|" + RealConversation.id;
                        ConvDetails["userid"] = ConvPart.userId;
                        ConvDetails["conversationid"] = RealConversation.id;
                        ConvDetails["acwstate"] = false;

                        string MediaType = string.Empty;
                        List<RealCN.Interaction> Interact = new List<RealCN.Interaction>();
                        if (ConvPart.calls != null)
                        {
                            Interact.AddRange(ConvPart.calls);
                            MediaType = "voice";
                        }
                        else if (ConvPart.callbacks != null)
                        {
                            Interact.AddRange(ConvPart.callbacks);
                            MediaType = "callback";
                        }
                        else if (ConvPart.emails != null)
                        {
                            Interact.AddRange(ConvPart.emails);
                            MediaType = "email";
                        }
                        else if (ConvPart.chats != null)
                        {
                            Interact.AddRange(ConvPart.chats);
                            MediaType = "chat";
                        }
                        else if (ConvPart.messages != null)
                        {
                            Interact.AddRange(ConvPart.messages);
                            MediaType = "message";
                        }
                        else
                        {
                            Console.WriteLine("Json Not a recognised Call Type\n{0}", JsonString);
                        }

                        if (Interact != null)
                        {
                            foreach (RealCN.Interaction ConvCall in Interact)
                            {
                                ConvDetails["Media"] = MediaType;
                                ConvDetails["Conversationstate"] = ConvCall.state;
                                if (MediaType == "callback")
                                    ConvDetails["Direction"] = "inbound";
                                else
                                    ConvDetails["Direction"] = ConvCall.direction;

                                ConvDetails["actingas"] = ConvPart.purpose;
                                ConvDetails["QueueId"] = ConvPart.queueId;
                                ConvDetails["updated"] = DateTime.UtcNow;

                                if (ConvPart.conversationRoutingData != null && ConvPart.conversationRoutingData.skills != null)
                                {
                                    if (ConvPart.conversationRoutingData.skills.Count() > 0)
                                        ConvDetails["skill1"] = ConvPart.conversationRoutingData.skills[0].id;
                                    if (ConvPart.conversationRoutingData.skills.Count() > 1)
                                        ConvDetails["skill2"] = ConvPart.conversationRoutingData.skills[1].id;
                                    if (ConvPart.conversationRoutingData.skills.Count() > 2)
                                        ConvDetails["skill3"] = ConvPart.conversationRoutingData.skills[2].id;
                                    ConvDetails["initialpriority"] = ConvPart.conversationRoutingData.priority;
                                }

                                if (ConvCall.state == "connected")
                                    if (ConvPart.connectedTime.HasValue == false || ConvPart.connectedTime.Value.Year < 2000)
                                        ConvDetails["talktime"] = DBNull.Value;
                                    else
                                    {
                                        ConvDetails["talktime"] = ConvPart.connectedTime;
                                        ConvDetails["talktimeltc"] = TimeZoneInfo.ConvertTimeFromUtc(ConvPart.connectedTime.Value, AppTimeZone);
                                    }
                                else
                                    ConvDetails["talktime"] = DBNull.Value;

                                if (ConvCall.afterCallWork != null)
                                {
                                    ConvDetails["acwstring"] = ConvCall.afterCallWork.state;
                                    switch (ConvCall.afterCallWork.state)
                                    {
                                        case "pending":
                                            if (ConvCall.afterCallWork.startTime.Year < 2000)
                                                ConvDetails["acwtime"] = DBNull.Value;
                                            else
                                                ConvDetails["acwtime"] = ConvCall.afterCallWork.startTime;
                                            ConvDetails["acwstate"] = true;
                                            break;
                                        case "completed":
                                            ConvDetails["acwtime"] = DBNull.Value;
                                            ConvDetails["acwstate"] = false;
                                            ConvDetails["Conversationstate"] = "terminated";
                                            break;
                                        default:
                                            ConvDetails["acwtime"] = DBNull.Value;
                                            ConvDetails["acwstate"] = false;
                                            break;
                                    }
                                }

                                if (ConvCall.held == false)
                                {
                                    ConvDetails["heldstate"] = ConvCall.held;
                                    ConvDetails["heldtime"] = DBNull.Value;
                                }
                                else
                                {
                                    if (ConvCall.startHoldTime.HasValue == false || ConvCall.startHoldTime.Value.Year < 2000)
                                        ConvDetails["heldtime"] = DBNull.Value;
                                    else
                                        ConvDetails["heldtime"] = ConvCall.startHoldTime.Value;
                                    ConvDetails["heldstate"] = ConvCall.held;
                                }
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Unknown Media Type");
                        }

                        if (RowFound == false)
                        {
                            DTUserCallsDets.Rows.Add(ConvDetails);
                        }
                        else
                        {
                            DTUserCallsDets.AcceptChanges();
                        }
                    }
                }
                WriteUserDataCallsDets = true;
            }
            WriteUserDataCallsDets = true;

            return Successful;
        }

        private Boolean TransQConv(string JsonString)
        {
            Boolean Successful = false;
            TimeZoneInfo AppTimeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneConfig);
            Console.Write("QClDets:");

            lock (DTQueueCallsDets)
            {
                RealCN.Conversations UserConvs = new RealCN.Conversations();
                UserConvs = JsonConvert.DeserializeObject<RealCN.Conversations>(JsonString,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    });

                RealCN.Eventbody RealConversation = UserConvs.eventBody;
                Console.Write("Conv={0}:", RealConversation.id.Substring(0, 5));

                foreach (RealCN.Participant ConvPart in RealConversation.participants)
                {
                    if (ConvPart.purpose == "acd" || ConvPart.purpose == "agent")
                    {
                        bool RowFound = false;
                        bool CallConnected = false;
                        DataRow ConvDetails = DTQueueCallsDets.Select("keyid = '" + RealConversation.id + "|" + ConvPart.queueId + "'").FirstOrDefault();

                        if (ConvDetails == null)
                        {
                            ConvDetails = DTQueueCallsDets.NewRow();
                        }
                        else
                        {
                            RowFound = true;
                        }

                        ConvDetails["keyid"] = RealConversation.id + "|" + ConvPart.queueId;
                        ConvDetails["userid"] = ConvPart.userId;
                        ConvDetails["conversationid"] = RealConversation.id;
                        ConvDetails["acwstate"] = false;

                        string MediaType = string.Empty;
                        List<RealCN.Interaction> Interact = new List<RealCN.Interaction>();
                        
                        if (ConvPart.calls != null)
                        {
                            foreach (RealCN.Interaction ConvCall in ConvPart.calls)
                            {
                                if (ConvCall.state == "connected")
                                {
                                    CallConnected = true;
                                }
                            }
                            
                            if (ConvPart.callbacks != null && CallConnected == false)
                            {
                                Interact.AddRange(ConvPart.callbacks);
                                MediaType = "callback";
                            }
                            else
                            {
                                Interact.AddRange(ConvPart.calls);
                                MediaType = "voice";
                            }
                        }
                        else if (ConvPart.callbacks != null)
                        {
                            Interact.AddRange(ConvPart.callbacks);
                            MediaType = "callback";
                        }
                        else if (ConvPart.emails != null)
                        {
                            Interact.AddRange(ConvPart.emails);
                            MediaType = "email";
                        }
                        else if (ConvPart.chats != null)
                        {
                            Interact.AddRange(ConvPart.chats);
                            MediaType = "chat";
                        }
                        else if (ConvPart.messages != null)
                        {
                            Interact.AddRange(ConvPart.messages);
                            MediaType = "message";
                        }
                        else
                        {
                            _logger.LogWarning("Json Not a recognised Call Type\n{0}", JsonString);
                        }

                        if (Interact != null)
                        {
                            foreach (RealCN.Interaction ConvCall in Interact)
                            {
                                ConvDetails["Media"] = MediaType;
                                ConvDetails["Conversationstate"] = ConvCall.state;
                                if (MediaType == "callback")
                                    ConvDetails["Direction"] = "inbound";
                                else
                                    ConvDetails["Direction"] = ConvCall.direction;

                                ConvDetails["actingas"] = ConvPart.purpose;
                                ConvDetails["QueueId"] = ConvPart.queueId;
                                ConvDetails["updated"] = DateTime.UtcNow;

                                if (ConvPart.conversationRoutingData != null && ConvPart.conversationRoutingData.skills != null)
                                {
                                    if (ConvPart.conversationRoutingData.skills.Count() > 0)
                                        ConvDetails["skill1"] = ConvPart.conversationRoutingData.skills[0].id;
                                    if (ConvPart.conversationRoutingData.skills.Count() > 1)
                                        ConvDetails["skill2"] = ConvPart.conversationRoutingData.skills[1].id;
                                    if (ConvPart.conversationRoutingData.skills.Count() > 2)
                                        ConvDetails["skill3"] = ConvPart.conversationRoutingData.skills[2].id;
                                    ConvDetails["initialpriority"] = ConvPart.conversationRoutingData.priority;
                                }

                                if (ConvCall.state == "connected")
                                    if (ConvPart.connectedTime == null || ConvPart.connectedTime.Value.Year < 2000)
                                        ConvDetails["talktime"] = DBNull.Value;
                                    else
                                    {
                                        ConvDetails["talktime"] = ConvPart.connectedTime;
                                        ConvDetails["talktimeltc"] = TimeZoneInfo.ConvertTimeFromUtc(ConvPart.connectedTime.Value, AppTimeZone);
                                    }
                                else
                                    ConvDetails["talktime"] = DBNull.Value;

                                if (ConvCall.afterCallWork != null)
                                {
                                    ConvDetails["acwstring"] = ConvCall.afterCallWork.state;
                                    switch (ConvCall.afterCallWork.state)
                                    {
                                        case "pending":
                                            if (ConvCall.afterCallWork.startTime.Year < 2000)
                                                ConvDetails["acwtime"] = DBNull.Value;
                                            else
                                                ConvDetails["acwtime"] = ConvCall.afterCallWork.startTime;
                                            ConvDetails["acwstate"] = true;
                                            break;
                                        case "completed":
                                            ConvDetails["acwtime"] = DBNull.Value;
                                            ConvDetails["acwstate"] = false;
                                            ConvDetails["Conversationstate"] = "terminated";
                                            break;
                                        default:
                                            ConvDetails["acwtime"] = DBNull.Value;
                                            ConvDetails["acwstate"] = false;
                                            break;
                                    }
                                }

                                if (ConvCall.held == false)
                                {
                                    ConvDetails["heldstate"] = ConvCall.held;
                                    ConvDetails["heldtime"] = DBNull.Value;
                                }
                                else
                                {
                                    if (ConvCall.startHoldTime == null || ConvCall.startHoldTime.Value.Year < 2000)
                                        ConvDetails["heldtime"] = DBNull.Value;
                                    else
                                        ConvDetails["heldtime"] = ConvCall.startHoldTime;
                                    ConvDetails["heldstate"] = ConvCall.held;
                                }
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Unknown Media Type");
                        }

                        if (RowFound == false)
                        {
                            DTQueueCallsDets.Rows.Add(ConvDetails);
                        }
                        else
                        {
                            DTQueueCallsDets.AcceptChanges();
                        }
                    }
                }
                WriteQueueDataCallsDets = true;
            }
            WriteQueueDataCallsDets = true;

            return Successful;
        }

        private Boolean TransAdherence(string JsonString)
        {
            Boolean Successful = false;
            Console.Write("Adh:");

            RealUA.Adherence UserAdherence = new RealUA.Adherence();
            UserAdherence = JsonConvert.DeserializeObject<RealUA.Adherence>(JsonString,
                        new JsonSerializerSettings
                        {
                            NullValueHandling = NullValueHandling.Ignore
                        });
            RealUA.EventbodyAdherence RealUserInfo = UserAdherence.eventBody;

            lock (DTUserData)
            {
                DataRow DRUserAct = DTUserData.Select("id = '" + RealUserInfo.user.id + "'").FirstOrDefault();

                if (DRUserAct != null)
                {
                    DRUserAct["id"] = RealUserInfo.user.id;
                    DRUserAct["adherenceState"] = RealUserInfo.adherenceState;
                    DRUserAct["adherencestarttime"] = RealUserInfo.adherenceChangeTime;
                    DRUserAct["impact"] = RealUserInfo.impact;
                    DRUserAct["scheduledActivityCategory"] = RealUserInfo.scheduledActivityCategory;
                }
                WriteUserDataAdh = true;
                Successful = true;
            }
            Successful = true;
            return Successful;
        }

        private Boolean TransCalls(string JsonString)
        {
            Boolean Successful = true;
            Console.Write("Cls:");

            RealUC.CallStats UserCalls = new RealUC.CallStats();
            UserCalls = JsonConvert.DeserializeObject<RealUC.CallStats>(JsonString,
                        new JsonSerializerSettings
                        {
                            NullValueHandling = NullValueHandling.Ignore
                        });
            RealUC.Eventbody RealUserInfo = UserCalls.eventBody;

            lock (DTUserData)
            {
                DataRow DRUserAct = DTUserData.Select("id = '" + UserCalls.topicName.Split('.')[2] + "'").FirstOrDefault();

                if (DRUserAct != null)
                {
                    DRUserAct["cccallactive"] = RealUserInfo.call.contactCenter.active;
                    DRUserAct["cccallacw"] = RealUserInfo.call.contactCenter.acw;

                    if (RealUserInfo.call.enterprise != null)
                    {
                        DRUserAct["othcallactive"] = RealUserInfo.call.enterprise.active;
                        DRUserAct["othcallacw"] = RealUserInfo.call.enterprise.acw; // Updated: use active property
                    }

                    DRUserAct["cbcallactive"] = RealUserInfo.callback.contactCenter.active;
                    DRUserAct["cbcallacw"] = RealUserInfo.callback.contactCenter.acw;

                    // Safe access for callback enterprise
                    if (RealUserInfo.callback.enterprise != null)
                    {
                        DRUserAct["cbothcallactive"] = RealUserInfo.callback.enterprise.active;
                        DRUserAct["cbothcallacw"] = RealUserInfo.callback.enterprise.acw; // Updated: use active property
                    }

                    DRUserAct["ccemailactive"] = RealUserInfo.email.contactCenter.active;
                    DRUserAct["ccemailacw"] = RealUserInfo.email.contactCenter.acw;

                    if (RealUserInfo.email.enterprise != null)
                    {
                        DRUserAct["othemailactive"] = RealUserInfo.email.enterprise.active;
                        DRUserAct["othemailacw"] = RealUserInfo.email.enterprise.active; // Updated: use active property
                    }

                    DRUserAct["ccchatactive"] = RealUserInfo.chat.contactCenter.active;
                    DRUserAct["ccchatacw"] = RealUserInfo.chat.contactCenter.acw;
           
                    if (RealUserInfo.chat.enterprise != null)
                    {
                        DRUserAct["othchatactive"] = RealUserInfo.chat.enterprise.active;
                        DRUserAct["othchatacw"] = RealUserInfo.chat.enterprise.active; // Updated: use active property
                    }
                }
                WriteUserDataCalls = true;
                Successful = true;
            }
            Console.Write("\nCls{0}Row(s):", DTUserData.Rows.Count);
            Successful = true;
            return Successful;
        }

        // Add methods for chunked subscriptions
        private void CreateUserActivitySubsForChunk(WebSocketDetail WebSock, DataTable userChunk)
        {
            Console.WriteLine($"Creating Activity Channel For {userChunk.Rows.Count} Users (Batch)");

            StringBuilder SubscriptionJSON = new StringBuilder();
            int Counter = 0;
            foreach (DataRow DRRow in userChunk.Rows)
            {
                SubscriptionJSON.Append(" \n{ \"id\": \"v2.users." + DRRow["id"].ToString() + ".activity\"},");
                ++Counter;
            }

            if (Counter > 0)
            {
                SubscriptionJSON.Length = SubscriptionJSON.Length - 1;
                string JSONBodyString = "[" + SubscriptionJSON.ToString() + " ]";
                string URL = "/api/v2/notifications/channels/" + WebSock.id + "/subscriptions";

                Console.WriteLine("API Key: {1} Activity Sock ID: {0}, Users: {2} ", WebSock.id, APIKey.Substring(0, 6), Counter);

                string JsonString = ChilKatJsonObj.ReturnJson(URL, JSONBodyString);
                if (string.IsNullOrEmpty(JsonString) || JsonString.Contains("error"))
                {
                    _logger?.LogError($"Failed to create activity subscription: {JsonString}");
                }
            }
        }

        private void CreateUserAdherenceSubsForChunk(WebSocketDetail WebSock, DataTable userChunk)
        {
            Console.WriteLine($"Creating Adherence Channel For {userChunk.Rows.Count} Users (Batch)");
       
            StringBuilder SubscriptionJSON = new StringBuilder();
            int Counter = 0;
            foreach (DataRow DRRow in userChunk.Rows)
            {
                SubscriptionJSON.Append(" \n{ \"id\": \"v2.users." + DRRow["id"].ToString() + ".workforcemanagement.adherence\"},");
                ++Counter;
            }

            if (Counter > 0)
            {
                SubscriptionJSON.Length = SubscriptionJSON.Length - 1;
                string JSONBodyString = "[" + SubscriptionJSON.ToString() + " ]";
                string URL = "/api/v2/notifications/channels/" + WebSock.id + "/subscriptions";

                Console.WriteLine("API Key: {1} Adherence Sock ID: {0}, Users: {2} ", WebSock.id, APIKey.Substring(0, 6), Counter);

                string JsonString = ChilKatJsonObj.ReturnJson(URL, JSONBodyString);
                if (string.IsNullOrEmpty(JsonString) || JsonString.Contains("error"))
                {
                    _logger?.LogError($"Failed to create adherence subscription: {JsonString}");
                }
            }
        }

        private void CreateUserCallSubsForChunk(WebSocketDetail WebSock, DataTable userChunk)
        {
            Console.WriteLine($"Creating Call Summary Channel For {userChunk.Rows.Count} Users (Batch)");

            StringBuilder SubscriptionJSON = new StringBuilder();
            int Counter = 0;
            foreach (DataRow DRRow in userChunk.Rows)
            {
                SubscriptionJSON.Append(" \n{ \"id\": \"v2.users." + DRRow["id"].ToString() + ".conversationsummary\"},");
                ++Counter;
            }

            if (Counter > 0)
            {
                SubscriptionJSON.Length = SubscriptionJSON.Length - 1;
                string JSONBodyString = "[" + SubscriptionJSON.ToString() + " ]";
                string URL = "/api/v2/notifications/channels/" + WebSock.id + "/subscriptions";

                Console.WriteLine("API Key: {1} Call Stats Sock ID: {0}, Users: {2} ", WebSock.id, APIKey.Substring(0, 6), Counter);

                string JsonString = ChilKatJsonObj.ReturnJson(URL, JSONBodyString);
                if (string.IsNullOrEmpty(JsonString) || JsonString.Contains("error"))
                {
                    _logger?.LogError($"Failed to create call summary subscription: {JsonString}");
                }
            }
        }

        private void CreateUserCallDetSubsForChunk(WebSocketDetail WebSock, DataTable userChunk)
        {
            Console.WriteLine($"Creating Call Details Channel For {userChunk.Rows.Count} Users (Batch)");

            StringBuilder SubscriptionJSON = new StringBuilder();
            int Counter = 0;
            foreach (DataRow DRRow in userChunk.Rows)
            {
                SubscriptionJSON.Append(" \n{ \"id\": \"v2.users." + DRRow["id"].ToString() + ".conversations\"},");
                ++Counter;
            }

            if (Counter > 0)
            {
                SubscriptionJSON.Length = SubscriptionJSON.Length - 1;
                string JSONBodyString = "[" + SubscriptionJSON.ToString() + " ]";
                string URL = "/api/v2/notifications/channels/" + WebSock.id + "/subscriptions";

                Console.WriteLine("API Key: {1} Call Details Sock ID: {0}, Users: {2} ", WebSock.id, APIKey.Substring(0, 6), Counter);

                string JsonString = ChilKatJsonObj.ReturnJson(URL, JSONBodyString);
                
                if (string.IsNullOrEmpty(JsonString) || JsonString.Contains("error"))
                {
                    _logger?.LogError($"Failed to create call details subscription: {JsonString}");
                }
            }
        }
        
        private void CreateQueueCallDetSubsForChunk(WebSocketDetail WebSock, DataTable queueChunk)
        {
            Console.WriteLine($"Creating Call Summary Channel For {queueChunk.Rows.Count} Queues (Batch)");

            StringBuilder SubscriptionJSON = new StringBuilder();
            int Counter = 0;
            foreach (DataRow DRRow in queueChunk.Rows)
            {
                SubscriptionJSON.Append(" \n{ \"id\": \"v2.routing.queues." + DRRow["id"].ToString() + ".conversations\"},");
                ++Counter;
            }
            Console.WriteLine("API Key: {1} Queue Call Details Sock ID: {0}, Queues: {2} ", WebSock.id, APIKey.Substring(0, 6), Counter);
            if (Counter > 0)
            {
                SubscriptionJSON.Length = SubscriptionJSON.Length - 1;
                string JSONBodyString = "[" + SubscriptionJSON.ToString() + " ]";
                string URL = "/api/v2/notifications/channels/" + WebSock.id + "/subscriptions";

                Console.WriteLine("API Key: {1} Queue Call Details Sock ID: {0}, Queues: {2} ", WebSock.id, APIKey.Substring(0, 6), Counter);

                string JsonString = ChilKatJsonObj.ReturnJson(URL, JSONBodyString);
                if (string.IsNullOrEmpty(JsonString) || JsonString.Contains("error"))
                {
                    _logger?.LogError($"Failed to create queue call details subscription: {JsonString}");
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _channelRefreshTimer?.Dispose();                
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void UserUpdate()
        {
            Console.WriteLine("UserUpdate method called");
            // Implement UserUpdate method
        }
    }

    public class WebSocketDetail
    {
        public string id { get; set; }
        public string connectUri { get; set; }
        public DateTime expires { get; set; }
        public string ReportName { get; set; }
        public bool IsExpired => DateTime.UtcNow > DateTime.Parse(expires.ToString());
        public TimeSpan TimeUntilExpiration => DateTime.Parse(expires.ToString()) - DateTime.UtcNow;
        public bool NeedsRefresh => TimeUntilExpiration < TimeSpan.FromHours(1);

        public override string ToString()
        {
            return $"Channel {id} for {ReportName}, expires in {TimeUntilExpiration.TotalHours:0.0} hours";
        }
    }

    public class AlertObject
    {
        public string topicName { get; set; }
        public string version { get; set; }
    }

    public static class WebSocketExtensions
    {
        // Updated method: now calls SendFrame directly with the message string.
        public static bool SendFrameFromString(this Chilkat.WebSocket webSocket, string message, string charset, bool compress)
        {
            return webSocket.SendFrame(message, compress);
        }
        
        // Updated SendString uses the new SendFrameFromString.
        public static bool SendString(this Chilkat.WebSocket webSocket, string message)
        {
            return webSocket.SendFrameFromString(message, "utf-8", true);
        }
    }
}
