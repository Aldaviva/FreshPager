using Bom.Squad;
using FreshPager.Data;
using Kasa;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Pager.Duty;
using Pager.Duty.Exceptions;
using Pager.Duty.Requests;
using Pager.Duty.Responses;
using Pager.Duty.Webhooks;
using Pager.Duty.Webhooks.Requests;
using System.Collections.Concurrent;
using ThrottleDebounce;
using Unfucked;
using Options = Kasa.Options;

const int MAX_PAGERDUTY_ATTEMPTS = 20;

BomSquad.DefuseUtf8Bom();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host
    .UseWindowsService()
    .UseSystemd();

builder.Services
    .Configure<Configuration>(builder.Configuration)
    .AddSingleton<PagerDutyFactory>(provider => key => new PagerDuty(key) { HttpClient = provider.GetRequiredService<HttpClient>() })
    .AddSingleton(provider => {
        int checkCount = provider.GetRequiredService<IOptions<Configuration>>().Value.pagerDutyIntegrationKeysByFreshpingCheckId.Count;
        return new ConcurrentDictionary<Check, string>(Math.Min(checkCount * 2, Environment.ProcessorCount), checkCount);
    })
    .AddSingleton<IKasaOutlet>(provider => provider.GetRequiredService<IOptions<Configuration>>() is
        { Value.alarmLightHostname: not "<IP address or FQDN of Kasa smart outlet>" and { } alarmLightHostname }
        ? new KasaOutlet(alarmLightHostname) { Options = new Options { LoggerFactory = provider.GetService<ILoggerFactory>() } } : null!)
    .AddSingleton<WebhookResource>(provider => provider.GetRequiredService<IOptions<Configuration>>() is { Value.pagerDutyWebhookSecrets: not ["<My PagerDuty webhook secret>"] and { } secrets }
        ? new WebhookResource(secrets) : null!)
    .AddHttpClient();

await using WebApplication webapp = builder.Build();

var configuration = webapp.Services.GetRequiredService<IOptions<Configuration>>();
var logger        = webapp.Services.GetRequiredService<ILogger<Program>>();
var dedupKeys     = webapp.Services.GetRequiredService<ConcurrentDictionary<Check, string>>();

#region Freshping

webapp.MapPost("/freshping", async Task<IResult> ([FromBody] FreshpingWebhookPayload payload, PagerDutyFactory pagerDutyFactory) => {
    Check check = payload.check;
    logger.LogTrace("Received webhook payload from Freshping: {payload}", payload);

    if (configuration.Value.pagerDutyIntegrationKeysByFreshpingCheckId.TryGetValue(check.id, out string? pagerDutyIntegrationKey)) {
        using IPagerDuty pagerDuty = pagerDutyFactory(pagerDutyIntegrationKey);

        if (payload.isCheckUp) {
            return await onCheckUp(check, pagerDuty);
        } else {
            return await onCheckDown(check, pagerDuty, payload);
        }
    } else {
        logger.LogWarning("No PagerDuty integration key configured for Freshping check {check}, not sending an alert to PagerDuty", check.name);
        return Results.NoContent();
    }
});

async Task<IResult> onCheckDown(Check check, IPagerDuty pagerDuty, FreshpingWebhookPayload requestBody) {
    logger.LogInformation("Freshping reports that {check} is down", check.name);
    dedupKeys.TryGetValue(check, out string? oldDedupKey);
    Uri reportUrl = new UrlBuilder("https", $"{requestBody.organizationSubdomain}.freshping.io").Path("reports").QueryParam("check_id", requestBody.checkId);

    try {
        TriggerAlert triggerAlert = new(Severity.Error, requestBody.eventTitle) {
            DedupKey  = oldDedupKey,
            Timestamp = requestBody.requestDateTime,
            Links = {
                new Link(reportUrl, "Freshping Report"),
                new Link(requestBody.checkedUrl, "Checked Service URL")
            },
            CustomDetails = new Dictionary<string, object> {
                ["Check Name"]              = requestBody.checkName,
                ["Request Location"]        = requestBody.requestLocation,
                ["Response Duration (sec)"] = requestBody.responseTime.TotalSeconds,
                ["Response State"]          = requestBody.responseState,
                ["Response Status Code"]    = requestBody.responseStatusCode?.ToString() ?? "(none)",
                ["Response Summary"]        = requestBody.responseSummary
            },

            // The following fields only appear on the webapp alert details page, and nowhere in the mobile app
            Class     = requestBody.responseSummary,
            Component = requestBody.checkedUrl.ToString(),
            Source    = requestBody.requestLocation
        };

        string newDedupKey = await Retrier.Attempt(async _ => {
            AlertResponse alertResponse = await pagerDuty.Send(triggerAlert);
            dedupKeys[check] = alertResponse.DedupKey;
            return alertResponse.DedupKey;
        }, MAX_PAGERDUTY_ATTEMPTS, retryDelay, isRetryAllowed);

        logger.LogInformation("Triggered alert in PagerDuty for {check} being down, got deduplication key {key}", check.name, newDedupKey);
    } catch (Exception e) when (e is not OutOfMemoryException) {
        logger.LogError(e, "Failed to trigger alert in PagerDuty after {attempts}, giving up", MAX_PAGERDUTY_ATTEMPTS);
        return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, detail: "Failed to trigger PagerDuty alert");
    }
    return Results.Created();
}

async Task<IResult> onCheckUp(Check check, IPagerDuty pagerDuty) {
    logger.LogInformation("Freshping reports that {check} is up", check.name);
    if (dedupKeys.TryRemove(check, out string? dedupKey)) {
        ResolveAlert resolution = new(dedupKey);
        await Retrier.Attempt(async _ => await pagerDuty.Send(resolution), MAX_PAGERDUTY_ATTEMPTS, retryDelay, isRetryAllowed);
        logger.LogInformation("Resolved PagerDuty alert for {check} being down using deduplication key {key}", check.name, dedupKey);
    } else {
        logger.LogWarning("No known PagerDuty alerts for check {check}, not resolving anything", check.name);
    }
    return Results.NoContent();
}

#endregion

#region PagerDuty

RequestDelegate webhookHandler;
if (webapp.Services.GetService<WebhookResource>() is { } webhookResource && webapp.Services.GetService<IKasaOutlet>() is { } kasa) {
    webhookHandler = webhookResource.HandlePostRequest;
    var allIncidentStatuses = new ConcurrentDictionary<Uri, IncidentStatus>();
    webhookResource.IncidentReceived += async (_, incident) => {
        if (incident.EventType is IncidentEventType.Triggered or IncidentEventType.Acknowledged or IncidentEventType.Unacknowledged or IncidentEventType.Resolved or IncidentEventType.Reopened) {
            allIncidentStatuses[incident.HtmlUrl] = incident.Status;
            bool isTriggered                  = incident.Status == IncidentStatus.Triggered;
            Uri? otherTriggeredIncidentWebUrl = isTriggered ? null : allIncidentStatuses.ToArray().FirstOrNull(entry => entry.Value == IncidentStatus.Triggered)?.Key ?? null;
            bool turnOn                       = isTriggered || otherTriggeredIncidentWebUrl != null;

            if (isTriggered || !turnOn) {
                logger.LogInformation("PagerDuty incident #{num:D} \"{title}\" is {status}, turning {onOff} {outlet}", incident.IncidentNumber, incident.Title, incident.Status, turnOn ? "on" : "off",
                    kasa.Hostname);
            } else {
                logger.LogInformation("PagerDuty incident #{num:D} \"{title}\" is {status}, but leaving {outlet} on because the other incident {otherUrl} is still triggered",
                    incident.IncidentNumber, incident.Title, incident.Status, kasa.Hostname, otherTriggeredIncidentWebUrl);
            }

            await kasa.System.SetSocketOn(turnOn);

            if (incident.Status == IncidentStatus.Resolved) {
                allIncidentStatuses.TryRemove(new KeyValuePair<Uri, IncidentStatus>(incident.HtmlUrl, incident.Status));
            }
        }
    };
    webhookResource.PingReceived += (_, _) => logger.LogInformation("Test webhook event received from PagerDuty");
} else {
    webhookHandler = context => {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return Task.CompletedTask;
    };
}
webapp.MapPost("/pagerduty", webhookHandler);

#endregion

await webapp.RunAsync();
return;

static TimeSpan retryDelay(int attempt) => TimeSpan.FromMinutes(attempt * attempt);
static bool isRetryAllowed(Exception exception) => exception is not (OutOfMemoryException or PagerDutyException { RetryAllowedAfterDelay: false });

internal delegate IPagerDuty PagerDutyFactory(string integrationKey);