using System.Text.Json.Serialization;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace FreshPager;

public class WebhookPayload {

    [JsonPropertyName("text")]
    public string text { get; set; }

    [JsonPropertyName("check_id")]
    public string checkId { get; set; }

    [JsonPropertyName("check_name")]
    public string checkName { get; set; }

    [JsonPropertyName("check_url")]
    public string checkUrl { get; set; }

    [JsonPropertyName("request_timeout")]
    public string requestTimeout { get; set; }

    [JsonPropertyName("request_location")]
    public string requestLocation { get; set; }

    [JsonPropertyName("request_datetime")]
    public DateTimeOffset requestDatetime { get; set; }

    [JsonPropertyName("response_status_code")]
    public string responseStatusCode { get; set; }

    [JsonPropertyName("response_summary")]
    public string responseSummary { get; set; }

    [JsonPropertyName("response_state")]
    public string responseState { get; set; }

    [JsonPropertyName("response_time")]
    public string responseTime { get; set; }

    [JsonPropertyName("event_data")]
    public EventData eventData { get; set; }

    public override string ToString() =>
        $"{nameof(text)}: {text}, {nameof(checkId)}: {checkId}, {nameof(checkName)}: {checkName}, {nameof(checkUrl)}: {checkUrl}, {nameof(requestTimeout)}: {requestTimeout}, {nameof(requestLocation)}: {requestLocation}, {nameof(requestDatetime)}: {requestDatetime}, {nameof(responseStatusCode)}: {responseStatusCode}, {nameof(responseSummary)}: {responseSummary}, {nameof(responseState)}: {responseState}, {nameof(responseTime)}: {responseTime}, {nameof(eventData)}: {eventData}";

    public class EventData {

        [JsonPropertyName("org_name")]
        public string orgName { get; set; }

        [JsonPropertyName("event_created_on")]
        public string eventCreatedOn { get; set; }

        [JsonPropertyName("event_id")]
        public int eventId { get; set; }

        [JsonPropertyName("org_id")]
        public int orgId { get; set; }

        [JsonPropertyName("webhook_type")]
        public string webhookType { get; set; }

        [JsonPropertyName("webhook_id")]
        public int webhookId { get; set; }

        public override string ToString() =>
            $"{nameof(orgName)}: {orgName}, {nameof(eventCreatedOn)}: {eventCreatedOn}, {nameof(eventId)}: {eventId}, {nameof(orgId)}: {orgId}, {nameof(webhookType)}: {webhookType}, {nameof(webhookId)}: {webhookId}";

    }

}