using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GCREalTime
{
    class QueueObsDef
    {
    }

}

namespace GenesysCloudDefQueueRealtime
{

    public class QueueRealTime
    {
        public Systemtoorganizationmappings systemToOrganizationMappings { get; set; }
        public Result[] results { get; set; }
    }

    public class Systemtoorganizationmappings
    {
        public string[] ON_QUEUE { get; set; }
        public string[] OFFLINE { get; set; }
        public string[] AVAILABLE { get; set; }
        public string[] BUSY { get; set; }
        public string[] MEETING { get; set; }
        public string[] TRAINING { get; set; }
        public string[] BREAK { get; set; }
    }

    public class Result
    {
        public Group group { get; set; }
        public Datum[] data { get; set; }
    }

    public class Group
    {
        public string queueId { get; set; }
        public string mediaType { get; set; }
    }

    public class Datum
    {
        public string metric { get; set; }
        public Stats stats { get; set; }
        public bool truncated { get; set; }
        public Observation[] observations { get; set; }
        public string qualifier { get; set; }
    }

    public class Stats
    {
        public int count { get; set; }
    }

    public class Observation
    {
        public DateTime observationDate { get; set; }
        public string conversationId { get; set; }
        public string sessionId { get; set; }
        public string[] requestedRoutingSkillIds { get; set; }
        public int routingPriority { get; set; }
        public string participantName { get; set; }
        public string direction { get; set; }
        public string convertedFrom { get; set; }
        public string[] requestedRoutings { get; set; }
        public string userId { get; set; }
        public string ani { get; set; }
        public string dnis { get; set; }
        public string usedRouting { get; set; }
        public string addressFrom { get; set; }
        public string requestedLanguageId { get; set; }
    }

}
// spell-checker: ignore: systemtoorganizationmappings