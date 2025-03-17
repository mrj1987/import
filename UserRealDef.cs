using Newtonsoft.Json;

namespace GenesysCloudDefUserRealtime
{

    public class UserRealTime
    {
        public Entity[] entities { get; set; }
        public int pageSize { get; set; }
        public int pageNumber { get; set; }
        public int total { get; set; }
        public string firstUri { get; set; }
        public string selfUri { get; set; }
        public string lastUri { get; set; }
        public int pageCount { get; set; }
    }

    public class Entity
    {
        public string id { get; set; }
        public string name { get; set; }
        public Division division { get; set; }
        public Chat chat { get; set; }
        public string department { get; set; }
        public string email { get; set; }
        public Primarycontactinfo[] primaryContactInfo { get; set; }
        public Address[] addresses { get; set; }
        public string state { get; set; }
        public string title { get; set; }
        public string username { get; set; }
        public Manager manager { get; set; }
        public Image[] images { get; set; }
        public int version { get; set; }
        public Routingstatus routingStatus { get; set; }
        public Presence presence { get; set; }
        public Conversationsummary conversationSummary { get; set; }
        public bool acdAutoAnswer { get; set; }
        public string selfUri { get; set; }
        public Geolocation geolocation { get; set; }
    }

    public class Division
    {
        public string id { get; set; }
        public string name { get; set; }
        public string selfUri { get; set; }
    }

    public class Chat
    {
        public string jabberId { get; set; }
    }

    public class Manager
    {
        public string id { get; set; }
        public string selfUri { get; set; }
    }

    public class Routingstatus
    {
        public string status { get; set; }
        public DateTime startTime { get; set; }
    }

    public class Presence
    {
        public string source { get; set; }
        public Presencedefinition presenceDefinition { get; set; }
        public string message { get; set; }
        public DateTime modifiedDate { get; set; }
    }

    public class Presencedefinition
    {
        public string id { get; set; }
        public string systemPresence { get; set; }
        public string selfUri { get; set; }
    }

    public class Conversationsummary
    {
        public Call call { get; set; }
        public Callback callback { get; set; }
        public Email email { get; set; }
        public Message message { get; set; }
        public Chat1 chat { get; set; }
        public Socialexpression socialExpression { get; set; }
        public Video video { get; set; }
    }

    public class Call
    {
        public Contactcenter contactCenter { get; set; }
        public Enterprise enterprise { get; set; }
    }

    public class Contactcenter
    {
        public int active { get; set; }
        public int acw { get; set; }
    }

    public class Enterprise
    {
        public int active { get; set; }
    }

    public class Callback
    {
        public Contactcenter1 contactCenter { get; set; }
        public Enterprise1 enterprise { get; set; }
    }

    public class Contactcenter1
    {
        public int active { get; set; }
        public int acw { get; set; }
    }

    public class Enterprise1
    {
        public int active { get; set; }
    }

    public class Email
    {
        public Contactcenter2 contactCenter { get; set; }
        public Enterprise2 enterprise { get; set; }
    }

    public class Contactcenter2
    {
        public int active { get; set; }
        public int acw { get; set; }
    }

    public class Enterprise2
    {
        public int active { get; set; }
    }

    public class Message
    {
        public Contactcenter3 contactCenter { get; set; }
        public Enterprise3 enterprise { get; set; }
    }

    public class Contactcenter3
    {
        public int active { get; set; }
        public int acw { get; set; }
    }

    public class Enterprise3
    {
        public int active { get; set; }
    }

    public class Chat1
    {
        public Contactcenter4 contactCenter { get; set; }
        public Enterprise4 enterprise { get; set; }
    }

    public class Contactcenter4
    {
        public int active { get; set; }
        public int acw { get; set; }
    }

    public class Enterprise4
    {
        public int active { get; set; }
    }

    public class Socialexpression
    {
        public Contactcenter5 contactCenter { get; set; }
        public Enterprise5 enterprise { get; set; }
    }

    public class Contactcenter5
    {
        public int active { get; set; }
        public int acw { get; set; }
    }

    public class Enterprise5
    {
        public int active { get; set; }
    }

    public class Video
    {
        public Contactcenter6 contactCenter { get; set; }
        public Enterprise6 enterprise { get; set; }
    }

    public class Contactcenter6
    {
        public int active { get; set; }
        public int acw { get; set; }
    }

    public class Enterprise6
    {
        public int active { get; set; }
    }

    public class Geolocation
    {
        public string id { get; set; }
        public string country { get; set; }
        public string region { get; set; }
        public string city { get; set; }
    }

    public class Primarycontactinfo
    {
        public string address { get; set; }
        public string mediaType { get; set; }
        public string type { get; set; }
        public string display { get; set; }
    }

    public class Address
    {
        public string display { get; set; }
        public string mediaType { get; set; }
        public string countryCode { get; set; }
        public string address { get; set; }
        public string type { get; set; }
    }

    public class Image
    {
        public string resolution { get; set; }
        public string imageUri { get; set; }
    }

}

namespace RealUserPushActivityDef
{

    public class Activity
    {
        public string topicName { get; set; }
        public string version { get; set; }
        public EventbodyUser eventBody { get; set; }
        public Metadata metadata { get; set; }
    }

    public class EventbodyUser
    {
        public string id { get; set; }
        public Routingstatus routingStatus { get; set; }
        public Presence presence { get; set; }
        public Outofoffice outOfOffice { get; set; }
        public string[] activeQueueIds { get; set; }
        public DateTime dateActiveQueuesChanged { get; set; }
    }

    public class Routingstatus
    {
        public string status { get; set; }
        public DateTime startTime { get; set; }
    }

    public class Presence
    {
        public Presencedefinition presenceDefinition { get; set; }
        public string presenceMessage { get; set; }
        public DateTime modifiedDate { get; set; }
    }

    public class Presencedefinition
    {
        public string id { get; set; }
        public string systemPresence { get; set; }
    }

    public class Outofoffice
    {
        public bool active { get; set; }
        public DateTime modifiedDate { get; set; }
    }

    public class Metadata
    {
        public string CorrelationId { get; set; }
    }

    public class Adherence
    {
        public string topicName { get; set; }
        public string version { get; set; }
        public EventbodyAdherence eventBody { get; set; }
        public Metadata metadata { get; set; }
    }

    public class EventbodyAdherence
    {
        public User user { get; set; }
        public string managementUnitId { get; set; }
        public string scheduledActivityCategory { get; set; }
        public string systemPresence { get; set; }
        public string organizationSecondaryPresenceId { get; set; }
        public string routingStatus { get; set; }
        public string actualActivityCategory { get; set; }
        public bool isOutOfOffice { get; set; }
        public string adherenceState { get; set; }
        public string impact { get; set; }
        public DateTime adherenceChangeTime { get; set; }
        public DateTime presenceUpdateTime { get; set; }
        public Activequeue[] activeQueues { get; set; }
        public DateTime activeQueuesModifiedTime { get; set; }
        public bool removedFromManagementUnit { get; set; }
    }

    public class User
    {
        public string id { get; set; }
    }

    public class Activequeue
    {
        public string id { get; set; }
    }

}

namespace RealUserPushCallStatsDef
{

    public class CallStats
    {
        public string topicName { get; set; }
        public string version { get; set; }
        public Eventbody eventBody { get; set; }
        public Metadata metadata { get; set; }
    }

    public class Eventbody
    {
        public Call call { get; set; }
        public Callback callback { get; set; }
        public Email email { get; set; }
        public Message message { get; set; }
        public Chat chat { get; set; }
        public Socialexpression socialExpression { get; set; }
        public Video video { get; set; }
    }

    public class Call
    {
        public Contactcenter contactCenter { get; set; }
        public Enterprise enterprise { get; set; }
    }

    public class Contactcenter
    {
        public int active { get; set; }
        public int acw { get; set; }
    }

    public class Enterprise
    {
        public int active { get; set; }
        public int acw { get; set; }
    }

    public class Callback
    {
        public Contactcenter1 contactCenter { get; set; }
        public Enterprise1 enterprise { get; set; }
    }

    public class Contactcenter1
    {
        public int active { get; set; }
        public int acw { get; set; }
    }

    public class Enterprise1
    {
        public int active { get; set; }
        public int acw { get; set; }
    }

    public class Email
    {
        public Contactcenter2 contactCenter { get; set; }
        public Enterprise2 enterprise { get; set; }
    }

    public class Contactcenter2
    {
        public int active { get; set; }
        public int acw { get; set; }
    }

    public class Enterprise2
    {
        public int active { get; set; }
        public int acw { get; set; }
    }

    public class Message
    {
        public Contactcenter3 contactCenter { get; set; }
        public Enterprise3 enterprise { get; set; }
    }

    public class Contactcenter3
    {
        public int active { get; set; }
        public int acw { get; set; }
    }

    public class Enterprise3
    {
        public int active { get; set; }
        public int acw { get; set; }
    }

    public class Chat
    {
        public Contactcenter4 contactCenter { get; set; }
        public Enterprise4 enterprise { get; set; }
    }

    public class Contactcenter4
    {
        public int active { get; set; }
        public int acw { get; set; }
    }

    public class Enterprise4
    {
        public int active { get; set; }
        public int acw { get; set; }
    }

    public class Socialexpression
    {
        public Contactcenter5 contactCenter { get; set; }
        public Enterprise5 enterprise { get; set; }
    }

    public class Contactcenter5
    {
        public int active { get; set; }
        public int acw { get; set; }
    }

    public class Enterprise5
    {
        public int active { get; set; }
        public int acw { get; set; }
    }

    public class Video
    {
        public Contactcenter6 contactCenter { get; set; }
        public Enterprise6 enterprise { get; set; }
    }

    public class Contactcenter6
    {
        public int active { get; set; }
        public int acw { get; set; }
    }

    public class Enterprise6
    {
        public int active { get; set; }
        public int acw { get; set; }
    }

    public class Metadata
    {
        public string CorrelationId { get; set; }
    }
}

namespace RealUserPushConversations
{
    public class Conversations
    {
        public string? topicName { get; set; }
        public string? version { get; set; }
        public Eventbody? eventBody { get; set; }
        public Metadata? metadata { get; set; }
    }

    public class Eventbody
    {
        // TODO: [JsonProperty(Required = Required.Always)]
        public string? id { get; set; }
        public Participant[]? participants { get; set; }
        public string? recordingState { get; set; }
        public string? address { get; set; }
    }

    public class Participant
    {
        // TODO: [JsonProperty(Required = Required.Always)]
        public string? id { get; set; }
        public DateTime? connectedTime { get; set; }
        public string? name { get; set; }
        public string? queueId { get; set; }
        public string? purpose { get; set; }
        public string? address { get; set; }
        public bool? wrapupRequired { get; set; }
        public bool? wrapupExpected { get; set; }
        public Attributes? attributes { get; set; }
        public Interaction[]? calls { get; set; }
        public Interaction[]? emails { get; set; }
        public Interaction[]? callbacks { get; set; }
        public Interaction[]? chats { get; set; }
        public Interaction[]? messages { get; set; }
        public Additionalproperties? additionalProperties { get; set; }
        public DateTime? endTime { get; set; }
        public Conversationroutingdata? conversationRoutingData { get; set; }
        public string? userId { get; set; }
        public string? wrapupPrompt { get; set; }
        public Wrapup? wrapup { get; set; }
        public DateTime? startAcwTime { get; set; }
        public DateTime? endAcwTime { get; set; }
        public int? alertingTimeoutMs { get; set; }
    }

    public class Attributes
    {
        public string? ivrSkills { get; set; }
        public string? ivrLanguageSkill { get; set; }
        public string? ivrPriority { get; set; }
    }

    public class Additionalproperties
    {
    }

    public class Conversationroutingdata
    {
        public Queue? queue { get; set; }
        public Language? language { get; set; }
        public int? priority { get; set; }
        public Skill[]? skills { get; set; }
    }

    public class Queue
    {
        // TODO: [JsonProperty(Required = Required.Always)]
        public string? id { get; set; }
    }

    public class Language
    {
        // TODO: [JsonProperty(Required = Required.Always)]
        public string? id { get; set; }
    }

    public class Skill
    {
        // TODO: [JsonProperty(Required = Required.Always)]
        public string? id { get; set; }
    }

    public class Wrapup
    {
        public string? code { get; set; }
        public string? notes { get; set; }
        public int? durationSeconds { get; set; }
        public DateTime? endTime { get; set; }
        public Additionalproperties1? additionalProperties { get; set; }
    }

    public class Additionalproperties1
    {
    }

    public class Interaction
    {
        public string? state { get; set; }
        // TODO: [JsonProperty(Required = Required.Always)]
        public string? id { get; set; }
        public string? disconnectType { get; set; }
        public bool? held { get; set; }
        public DateTime? startHoldTime { get; set; }
        public DateTime? endHoldTime { get; set; }
        public string[]? callbackNumbers { get; set; }
        public string? callbackUserName { get; set; }
        public string? scriptId { get; set; }
        public string? peerId { get; set; }
        public string? direction { get; set; }
        public bool? externalCampaign { get; set; }
        public bool? skipEnabled { get; set; }
        public string? provider { get; set; }
        public int? timeoutSeconds { get; set; }
        public DateTime? connectedTime { get; set; }
        public DateTime? disconnectedTime { get; set; }
        public bool? afterCallWorkRequired { get; set; }
        public Aftercallwork? afterCallWork { get; set; }
        public int? messagesSent { get; set; }
        public string? messageId { get; set; }
        public Disconnectreason[]? disconnectReasons { get; set; }
    }

    public class Callback
    {
        public string state { get; set; }
        public string id { get; set; }
        public string disconnectType { get; set; }
        public bool held { get; set; }
        public DateTime startHoldTime { get; set; }
        public DateTime endHoldTime { get; set; }
        public string[] callbackNumbers { get; set; }
        public string callbackUserName { get; set; }
        public string scriptId { get; set; }
        public string peerId { get; set; }
        public string direction { get; set; }
        public bool externalCampaign { get; set; }
        public bool skipEnabled { get; set; }
        public string provider { get; set; }
        public int timeoutSeconds { get; set; }
        public DateTime connectedTime { get; set; }
        public DateTime disconnectedTime { get; set; }
        public bool afterCallWorkRequired { get; set; }
        public Aftercallwork afterCallWork { get; set; }
    }

    public class Chat
    {
        public string state { get; set; }
        public string id { get; set; }
        public string disconnectType { get; set; }
        public bool held { get; set; }
        public DateTime startHoldTime { get; set; }
        public DateTime endHoldTime { get; set; }
        public string[] callbackNumbers { get; set; }
        public string callbackUserName { get; set; }
        public string scriptId { get; set; }
        public string peerId { get; set; }
        public string direction { get; set; }
        public bool externalCampaign { get; set; }
        public bool skipEnabled { get; set; }
        public string provider { get; set; }
        public int timeoutSeconds { get; set; }
        public DateTime connectedTime { get; set; }
        public DateTime disconnectedTime { get; set; }
        public bool afterCallWorkRequired { get; set; }
        public Aftercallwork afterCallWork { get; set; }
    }

    public class Email
    {
        public string id { get; set; }
        public string state { get; set; }
        public bool held { get; set; }
        public DateTime startHoldTime { get; set; }
        public DateTime endHoldTime { get; set; }
        public bool autoGenerated { get; set; }
        public string provider { get; set; }
        public string peerId { get; set; }
        public int messagesSent { get; set; }
        public DateTime connectedTime { get; set; }
        public string messageId { get; set; }
        public string direction { get; set; }
        public bool spam { get; set; }
        public bool afterCallWorkRequired { get; set; }
        public Additionalproperties1 additionalProperties { get; set; }
        public string disconnectType { get; set; }
        public DateTime disconnectedTime { get; set; }
        public Disconnectreason[] disconnectReasons { get; set; }
        public Errorinfo errorInfo { get; set; }
        public string scriptId { get; set; }
        public Aftercallwork afterCallWork { get; set; }
    }

    public class Call
    {
        public string id { get; set; }
        public string state { get; set; }
        public bool recording { get; set; }
        public string recordingState { get; set; }
        public bool muted { get; set; }
        public bool confined { get; set; }
        public bool held { get; set; }
        public DateTime startHoldTime { get; set; }
        public DateTime endHoldTime { get; set; }
        public string direction { get; set; }
        public Self self { get; set; }
        public Other other { get; set; }
        public string provider { get; set; }
        public DateTime connectedTime { get; set; }
        public bool afterCallWorkRequired { get; set; }
        public Additionalproperties4 additionalProperties { get; set; }
        public string disconnectType { get; set; }
        public string peerId { get; set; }
        public DateTime disconnectedTime { get; set; }
        public Disconnectreason[] disconnectReasons { get; set; }
        public Errorinfo errorInfo { get; set; }
        public string scriptId { get; set; }
        public Aftercallwork afterCallWork { get; set; }
    }

    public class Self
    {
        public string name { get; set; }
        public string nameRaw { get; set; }
        public string addressNormalized { get; set; }
        public string addressRaw { get; set; }
        public string addressDisplayable { get; set; }
        public Additionalproperties2 additionalProperties { get; set; }
    }

    public class Additionalproperties2
    {
    }

    public class Other
    {
        public string name { get; set; }
        public string nameRaw { get; set; }
        public string addressNormalized { get; set; }
        public string addressRaw { get; set; }
        public string addressDisplayable { get; set; }
        public Additionalproperties3 additionalProperties { get; set; }
    }

    public class Additionalproperties3
    {
    }

    public class Additionalproperties4
    {
    }

    public class Errorinfo
    {
        public string code { get; set; }
        public string message { get; set; }
        public string messageWithParams { get; set; }
        public Messageparams messageParams { get; set; }
        public Additionalproperties5 additionalProperties { get; set; }
    }

    public class Messageparams
    {
        public string type { get; set; }
        public string sessionId { get; set; }
    }

    public class Additionalproperties5
    {
    }

    public class Aftercallwork
    {
        public string state { get; set; }
        public DateTime startTime { get; set; }
        public DateTime endTime { get; set; }
    }

    public class Disconnectreason
    {
        public string type { get; set; }
        public int code { get; set; }
        public string phrase { get; set; }
    }

    public class Metadata
    {
        public string CorrelationId { get; set; }
    }


}

namespace RealQueuePushConversations
{

    public class Conversations
    {
        public string topicName { get; set; }
        public string version { get; set; }
        public Eventbody eventBody { get; set; }
        public Metadata metadata { get; set; }
    }

    public class Eventbody
    {
        public string id { get; set; }
        public Participant[] participants { get; set; }
        public string recordingState { get; set; }
        public string address { get; set; }
    }

    public class Participant
    {
        public string id { get; set; }
        public DateTime connectedTime { get; set; }
        public DateTime endTime { get; set; }
        public string name { get; set; }
        public string queueId { get; set; }
        public string purpose { get; set; }
        public string address { get; set; }
        public bool wrapupRequired { get; set; }
        public bool wrapupExpected { get; set; }
        public Attributes attributes { get; set; }
        public Interaction[] calls { get; set; }
        public Interaction[] emails { get; set; }
        public Interaction[] callbacks { get; set; }
        public Interaction[] chats { get; set; }
        public Interaction[] messages { get; set; }
        public Additionalproperties additionalProperties { get; set; }
        public Conversationroutingdata conversationRoutingData { get; set; }
        public string userId { get; set; }
        public string wrapupPrompt { get; set; }
        public int wrapupTimeoutMs { get; set; }
        public Wrapup wrapup { get; set; }
        public DateTime startAcwTime { get; set; }
        public DateTime endAcwTime { get; set; }
        public int alertingTimeoutMs { get; set; }
    }

    public class Attributes
    {
        public string ivrSkills { get; set; }
        public string ivrLanguageSkill { get; set; }
        public string ivrPriority { get; set; }
    }

    public class Additionalproperties
    {
    }

    public class Conversationroutingdata
    {
        public Queue queue { get; set; }
        public Language language { get; set; }
        public int priority { get; set; }
        public Skill[] skills { get; set; }
    }

    public class Queue
    {
        public string id { get; set; }
    }

    public class Language
    {
    }

    public class Skill
    {
        public string id { get; set; }
    }

    public class Wrapup
    {
        public string code { get; set; }
        public int durationSeconds { get; set; }
        public DateTime endTime { get; set; }
        public Additionalproperties1 additionalProperties { get; set; }
    }

    public class Additionalproperties1
    {
    }

    public class Interaction
    {
        public string state { get; set; }
        public string id { get; set; }
        public string disconnectType { get; set; }
        public bool held { get; set; }
        public DateTime startHoldTime { get; set; }
        public DateTime endHoldTime { get; set; }
        public string[] callbackNumbers { get; set; }
        public string callbackUserName { get; set; }
        public string scriptId { get; set; }
        public string peerId { get; set; }
        public string direction { get; set; }
        public bool externalCampaign { get; set; }
        public bool skipEnabled { get; set; }
        public string provider { get; set; }
        public int timeoutSeconds { get; set; }
        public DateTime connectedTime { get; set; }
        public DateTime disconnectedTime { get; set; }
        public bool afterCallWorkRequired { get; set; }
        public Aftercallwork afterCallWork { get; set; }
        public int messagesSent { get; set; }
        public string messageId { get; set; }
        public Disconnectreason[] disconnectReasons { get; set; }
    }

    public class Email
    {
        public string id { get; set; }
        public string state { get; set; }
        public bool held { get; set; }
        public DateTime startHoldTime { get; set; }
        public DateTime endHoldTime { get; set; }
        public bool autoGenerated { get; set; }
        public string provider { get; set; }
        public string peerId { get; set; }
        public int messagesSent { get; set; }
        public DateTime connectedTime { get; set; }
        public string messageId { get; set; }
        public string direction { get; set; }
        public bool spam { get; set; }
        public bool afterCallWorkRequired { get; set; }
        public Additionalproperties1 additionalProperties { get; set; }
        public string disconnectType { get; set; }
        public DateTime disconnectedTime { get; set; }
        public Disconnectreason[] disconnectReasons { get; set; }
        public Errorinfo errorInfo { get; set; }
        public string scriptId { get; set; }
        public Aftercallwork afterCallWork { get; set; }
    }

    public class Call
    {
        public string id { get; set; }
        public string state { get; set; }
        public bool recording { get; set; }
        public string recordingState { get; set; }
        public bool muted { get; set; }
        public bool confined { get; set; }
        public bool held { get; set; }
        public string disconnectType { get; set; }
        public string direction { get; set; }
        public Self self { get; set; }
        public Other other { get; set; }
        public string provider { get; set; }
        public DateTime connectedTime { get; set; }
        public DateTime disconnectedTime { get; set; }
        public Disconnectreason[] disconnectReasons { get; set; }
        public bool afterCallWorkRequired { get; set; }
        public Additionalproperties4 additionalProperties { get; set; }
        public string peerId { get; set; }
        public string scriptId { get; set; }
        public Aftercallwork afterCallWork { get; set; }
    }

    public class Self
    {
        public string name { get; set; }
        public string nameRaw { get; set; }
        public string addressNormalized { get; set; }
        public string addressRaw { get; set; }
        public string addressDisplayable { get; set; }
        public Additionalproperties2 additionalProperties { get; set; }
    }

    public class Additionalproperties2
    {
    }

    public class Other
    {
        public string name { get; set; }
        public string nameRaw { get; set; }
        public string addressNormalized { get; set; }
        public string addressRaw { get; set; }
        public string addressDisplayable { get; set; }
        public Additionalproperties3 additionalProperties { get; set; }
    }

    public class Additionalproperties3
    {
    }

    public class Additionalproperties4
    {
    }

    public class Aftercallwork
    {
        public string state { get; set; }
        public DateTime startTime { get; set; }
        public DateTime endTime { get; set; }
    }

    public class Disconnectreason
    {
        public string type { get; set; }
        public int code { get; set; }
        public string phrase { get; set; }
    }

    public class Metadata
    {
        public string CorrelationId { get; set; }
    }

    public class Errorinfo
    {
        public string code { get; set; }
        public string message { get; set; }
        public string messageWithParams { get; set; }
        public Messageparams messageParams { get; set; }
        public Additionalproperties5 additionalProperties { get; set; }
    }

    public class Messageparams
    {
        public string type { get; set; }
        public string sessionId { get; set; }
    }

    public class Additionalproperties5
    {
    }

}
namespace RealQueuePushObservations
{

    public class Observations
    {
        public string topicName { get; set; }
        public string version { get; set; }
        public Eventbody eventBody { get; set; }
        public Metadata metadata { get; set; }
    }
    public class Metadata
    {
        public string CorrelationId { get; set; }
    }
    public class Eventbody
    {
        public string id { get; set; }
        public Data[] data { get; set; }
        public Group group { get; set; }
        public string recordingState { get; set; }
        public string address { get; set; }
    }
    public class Stats
    {
        public double count { get; set; }
    }

    public class Observation
    {
        public DateTime observationDate { get; set; }
        public string conversationId { get; set; }
        public string sessionId { get; set; }
        public int routingPriority { get; set; }
        public string participantName { get; set; }
        public string userId { get; set; }
        public string direction { get; set; }
        public string ani { get; set; }
        public string dnis { get; set; }
    }

    public class Data
    {
        public string interval { get; set; } // Added interval property
        public Metrics[] metrics { get; set; } // Changed Observation[] to Metrics[] to match JSON structure
        public Observation[] observations { get; set; }
    }

    public class Metrics
    {
        public string metric { get; set; }
        public Stats stats { get; set; }
    }

    public class Group
    {
        public string queueId { get; set; }
        public string mediaType { get; set; }
    }

    public class Result
    {
        public Group group { get; set; }
        public Data[] data { get; set; }
    }
    
}
// spell-checker: ignore: outofoffice