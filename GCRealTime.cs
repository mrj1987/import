using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using StandardUtils;

namespace GCRealTime
{
    public class GCRealTime
    {
        private readonly ILogger _logger;
        private readonly TelemetryClient _telemetry;
        private List<Thread> allThreads = new List<Thread>();

        public GCRealTime(ILogger logger, TelemetryClient telemetry)
        {
            _logger = logger;
            _telemetry = telemetry;
        }

        public void RunRealTime()
        {
            Utils Utils = new Utils();
            _logger.LogInformation("RealTime Start " + DateTime.Now);
            
            UserRealTime UserAct = new UserRealTime(_logger);
            UserAct.SyncType = "userActivity";
            UserAct.Initialize();

            Thread ThUserAct = new Thread(new ThreadStart(UserAct.StartUserActivity));
            ThUserAct.Start();
            allThreads.Add(ThUserAct);
            
            Thread ThUserAdh = new Thread(new ThreadStart(UserAct.StartUserAdherence));
            ThUserAdh.Start();
            allThreads.Add(ThUserAdh);

            Thread ThUserCall = new Thread(new ThreadStart(UserAct.StartUserCalls));
            ThUserCall.Start();
            allThreads.Add(ThUserCall);

            UserRealTime UserRealTime = new UserRealTime(_logger);
            UserRealTime.SyncType = "userCalls";
            UserRealTime.Initialize();

            Thread ThUserCallDets = new Thread(new ThreadStart(UserRealTime.StartUserCallDets));
            ThUserCallDets.Start();
            allThreads.Add(ThUserCallDets);

            UserRealTime QVRealTime = new UserRealTime(_logger);
            QVRealTime.SyncType = "queueCalls";
            QVRealTime.Initialize();

            Thread ThQueueCallDets = new Thread(new ThreadStart(QVRealTime.StartQueueCallDets));
            ThQueueCallDets.Start();
            allThreads.Add(ThQueueCallDets);

            // Track additional threads created for chunking
            if (UserAct.WebSocketThreads?.Count > 0)
                allThreads.AddRange(UserAct.WebSocketThreads);
            if (UserRealTime.WebSocketThreads?.Count > 0)
                allThreads.AddRange(UserRealTime.WebSocketThreads);
            if (QVRealTime.WebSocketThreads?.Count > 0)
                allThreads.AddRange(QVRealTime.WebSocketThreads);

            var runningTime = System.Diagnostics.Stopwatch.StartNew();
            var reportTimer = System.Diagnostics.Stopwatch.StartNew();
            while (true)
            {
                Thread.Sleep(10000);
                if (reportTimer.Elapsed > TimeSpan.FromMinutes(10))
                {
                    _logger?.LogInformation("Realtime reporting heartbeat");
                    var props = new Dictionary<string,string>{};
                    var metrics = new Dictionary<string,double>{{"RunningTime", runningTime.Elapsed.TotalSeconds}};
                    _telemetry?.TrackEvent("Heartbeat", props, metrics);
                    reportTimer.Restart();
                }

                _logger.LogInformation("Realtime running for {0}", runningTime.Elapsed);
                Console.WriteLine("Thread User Act        Status :{0}, errors {1}", ThUserAct.ThreadState, UserAct.TotalErrors);
                Console.WriteLine("Thread User Adh        Status :{0}", ThUserAdh.ThreadState);
                Console.WriteLine("Thread User Call       Status :{0}", ThUserCall.ThreadState);
                Console.WriteLine("Thread User Call Dets  Status :{0}, errors {1}", ThUserCallDets.ThreadState, UserRealTime.TotalErrors);
                Console.WriteLine("Thread Queue Call Dets Status :{0}, errors {1}", ThQueueCallDets.ThreadState, QVRealTime.TotalErrors);

                // Check for stopped threads including chunking threads
                bool anyThreadStopped = false;
                foreach(var thread in allThreads)
                {
                    if (thread.ThreadState == ThreadState.Stopped || thread.ThreadState == ThreadState.Aborted)
                    {
                        anyThreadStopped = true;
                        _logger.LogWarning($"Thread {thread.Name ?? "Unknown"} state is {thread.ThreadState}");
                    }
                }

                if (UserAct.ShouldExit || UserRealTime.ShouldExit || QVRealTime.ShouldExit || anyThreadStopped)
                {
                    if (UserAct.ShouldExit || UserRealTime.ShouldExit || QVRealTime.ShouldExit)
                        _logger.LogInformation("Exit requested");
                    else
                        _logger.LogWarning("Exiting due to thread stopped");

                    UserAct.ShouldExit = true;
                    UserRealTime.ShouldExit = true;
                    QVRealTime.ShouldExit = true;
                    
                    // Join all threads with timeout
                    foreach(var thread in allThreads)
                    {
                        thread.Join(TimeSpan.FromSeconds(30));
                    }
                    break;
                }
            }
        }
    }
}
