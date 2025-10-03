using FreshPager.Data.Marshal;
using System.Text.Json.Serialization;

// ReSharper disable UnusedAutoPropertyAccessor.Global - these are called by the JSON deserializer

namespace FreshPager.Data;

public class FreshpingWebhookPayload {

    /// <summary>
    /// <para>The title/subject/summary of the event</para>
    /// <para>Examples:</para>
    /// <para>Aldaviva HTTP (https://aldaviva.com) is DOWN.</para>
    /// <para>Aldaviva SMTP (tcp://aldaviva.com:25) is UP.</para>
    /// </summary>
    [JsonPropertyName("text")]
    public required string eventTitle { get; init; }

    /// <summary>
    /// <para>Numeric ID of the check</para>
    /// <para>Examples:</para>
    /// <para>36897</para>
    /// <para>829684</para>
    /// </summary>
    [JsonPropertyName("check_id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int checkId { get; init; }

    /// <summary>
    /// <para>The friendly check name</para>
    /// <para>Examples:</para>
    /// <para>Aldaviva HTTP</para>
    /// <para>Aldaviva SMTP</para>
    /// </summary>
    [JsonPropertyName("check_name")]
    public required string checkName { get; init; }

    public FreshpingCheck check => new(checkId, checkName);

    /// <summary>
    /// <para>The URL that was hit to do the health check</para>
    /// <para>Examples:</para>
    /// <para>https://aldaviva.com</para>
    /// <para>tcp://aldaviva.com:25</para>
    /// </summary>
    [JsonPropertyName("check_url")]
    public required Uri checkedUrl { get; init; }

    /// <summary>
    /// <para>How long the health check had been willing to wait for a response (not how long it actually waited)</para>
    /// <para>Example:</para>
    /// <para>0:30:00</para>
    /// </summary>
    [JsonPropertyName("request_timeout")]
    [JsonConverter(typeof(StringToTimespanConverter.FromSeconds))]
    public TimeSpan requestTimeout { get; init; }

    /// <summary>
    /// <para>Examples:</para>
    /// <para>US East (N. Virginia)</para>
    /// <para>Asia Pacific (Tokyo)</para>
    /// <para>EU (Ireland)</para>
    /// <para>Asia Pacific (Singapore)</para>
    /// <para>Canada (Central)</para>
    /// <para>Asia Pacific (Sydney)</para>
    /// <para>US West (Oregon)</para>
    /// <para>Asia Pacific (Mumbai)</para>
    /// <para>South America (Sao Paulo)</para>
    /// <para>EU (London)</para>
    /// </summary>
    [JsonPropertyName("request_location")]
    public required string requestLocation { get; init; }

    /// <summary>
    /// <para>Example:</para>
    /// <para>2024-06-28T18:07:46.709971+00:00</para>
    /// </summary>
    [JsonPropertyName("request_datetime")]
    public DateTimeOffset requestDateTime { get; init; }

    /// <summary>
    /// <para>Examples:</para>
    /// <para><c>None</c> (check is down due to a socket/processing exception like a timeout)</para>
    /// <para><c>200</c> (HTTP check is up)</para>
    /// <para><c>1</c> (TCP check is up)</para>
    /// </summary>
    [JsonPropertyName("response_status_code")]
    [JsonConverter(typeof(StringToOptionalIntConverter))]
    public int? responseStatusCode { get; init; }

    /// <summary>
    /// <para>Examples:</para>
    /// <para>Connection Timeout</para>
    /// <para>Not Responding</para>
    /// <para>Available</para>
    /// </summary>
    [JsonPropertyName("response_summary")]
    public required string responseSummary { get; init; }

    public bool isCheckUp => responseSummary == "Available";

    /// <summary>
    /// <para>Examples:</para>
    /// <para>Not Responding</para>
    /// <para>Available</para>
    /// </summary>
    [JsonPropertyName("response_state")]
    public required string responseState { get; init; }

    /// <summary>
    /// <para>How long it actually took for the health check to get a response.</para>
    /// <para>For the maximum time the check was willing to wait, see <see cref="requestTimeout"/>.</para>
    /// <para>Examples:</para>
    /// <para>30003</para>
    /// <para>17</para>
    /// </summary>
    [JsonPropertyName("response_time")]
    [JsonConverter(typeof(StringToTimespanConverter.FromMilliseconds))]
    public TimeSpan responseTime { get; init; }

    [JsonPropertyName("event_data")]
    [JsonInclude]
    private EventData eventData { get; set; } = null!;

    public string organizationSubdomain => eventData.organizationSubdomain;
    public DateTimeOffset eventCreationDateTime => eventData.eventCreationDateTime;
    public int eventId => eventData.eventId;
    public int organizationId => eventData.organizationId;
    public int webhookId => eventData.webhookId;
    public EventFilter eventFilter => eventData.eventFilter;

    public override string ToString() =>
        $"{nameof(eventTitle)}: {eventTitle}, {nameof(checkId)}: {checkId}, {nameof(checkName)}: {checkName}, {nameof(checkedUrl)}: {checkedUrl}, {nameof(requestTimeout)}: {requestTimeout}, {nameof(requestLocation)}: {requestLocation}, {nameof(requestDateTime)}: {requestDateTime}, {nameof(responseStatusCode)}: {responseStatusCode}, {nameof(responseSummary)}: {responseSummary}, {nameof(isCheckUp)}: {isCheckUp}, {nameof(responseState)}: {responseState}, {nameof(responseTime)}: {responseTime}, {nameof(organizationSubdomain)}: {organizationSubdomain}, {nameof(eventCreationDateTime)}: {eventCreationDateTime}, {nameof(eventId)}: {eventId}, {nameof(organizationId)}: {organizationId}, {nameof(webhookId)}: {webhookId}, {nameof(eventFilter)}: {eventFilter}";

    public class EventData {

        /// <summary>
        /// <para>The subdomain of the Freshping organization, also known as Freshping URL (not the Account Name) </para>
        /// <para>Examples:</para>
        /// <para>aldaviva</para>
        /// </summary>
        [JsonPropertyName("org_name")]
        public required string organizationSubdomain { get; init; }

        /// <summary>
        /// <para>Example:</para>
        /// <para>2024-06-27T23:49:30.033405+00:00</para>
        /// </summary>
        [JsonPropertyName("event_created_on")]
        public DateTimeOffset eventCreationDateTime { get; init; }

        /// <summary>
        /// <para>Unique ID for each webhook message sent</para>
        /// <para>Examples:</para>
        /// <para>17960894</para>
        /// <para>17960760</para>
        /// </summary>
        [JsonPropertyName("event_id")]
        public int eventId { get; init; }

        /// <summary>
        /// <para>Example:</para>
        /// <para>10593</para>
        /// </summary>
        [JsonPropertyName("org_id")]
        public int organizationId { get; init; }

        /// <summary>
        /// <para>Examples:</para>
        /// <para><c>AL</c> (all events)</para>
        /// <para><c>AT</c> (up and down events)</para>
        /// <para><c>PE</c> (performance degraded events)</para>
        /// <para><c>PS</c> (paused and restarted events)</para>
        /// </summary>
        [JsonPropertyName("webhook_type")]
        [JsonInclude]
        private string eventFilterRaw { get; init; } = null!;

        /// <exception cref="ArgumentOutOfRangeException" accessor="get"></exception>
        public EventFilter eventFilter => eventFilterRaw switch {
            "AL" => EventFilter.ALL,
            "AT" => EventFilter.UP_DOWN,
            "PE" => EventFilter.DEGRADED_PERFORMANCE,
            "PS" => EventFilter.PAUSED_UNPAUSED,
            _    => throw new ArgumentOutOfRangeException(nameof(eventFilterRaw), eventFilterRaw, $"Unrecognized webhook_type value '{eventFilterRaw}'")
        };

        /// <summary>
        /// <para>The unique ID of the webhook integration</para>
        /// <para>Example:</para>
        /// <para>35191</para>
        /// </summary>
        [JsonPropertyName("webhook_id")]
        public int webhookId { get; init; }

    }

    public enum EventFilter {

        ALL,
        UP_DOWN,
        DEGRADED_PERFORMANCE,
        PAUSED_UNPAUSED

    }

}