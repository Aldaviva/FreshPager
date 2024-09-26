using Bom.Squad;
using FreshPager.Data;
using jaytwo.FluentUri;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Pager.Duty;
using Pager.Duty.Exceptions;
using Pager.Duty.Requests;
using Pager.Duty.Responses;
using System.Collections.Concurrent;
using ThrottleDebounce;

const uint MAX_PAGERDUTY_ATTEMPTS = 20;

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
    .AddHttpClient();

await using WebApplication webapp = builder.Build();

var dedupKeys = webapp.Services.GetRequiredService<ConcurrentDictionary<Check, string>>();
var logger    = webapp.Services.GetRequiredService<ILogger<Program>>();

webapp.MapPost("/", async Task<IResult> ([FromBody] WebhookPayload payload, PagerDutyFactory pagerDutyFactory, IOptions<Configuration> configuration) => {
    Check check = payload.check;
    logger.LogTrace("Received webhook payload from Freshping: {payload}", payload);

    if (configuration.Value.pagerDutyIntegrationKeysByFreshpingCheckId.TryGetValue(check.id, out string? pagerDutyIntegrationKey)) {
        using IPagerDuty pagerDuty = pagerDutyFactory(pagerDutyIntegrationKey);

        if (!payload.isCheckUp) {
            return await onCheckDown(check, pagerDuty, payload);
        } else {
            return await onCheckUp(check, pagerDuty);
        }
    } else {
        logger.LogWarning("No PagerDuty integration key configured for Freshping check {check}, not sending an alert to PagerDuty", check.name);
        return Results.NoContent();
    }
});

async Task<IResult> onCheckDown(Check check, IPagerDuty pagerDuty, WebhookPayload requestBody) {
    logger.LogInformation("Freshping reports that {check} is down", check.name);
    dedupKeys.TryGetValue(check, out string? oldDedupKey);
    Uri reportUrl = new UriBuilder("https", $"{requestBody.organizationSubdomain}.freshping.io", -1, "reports").Uri.WithQueryParameter("check_id", requestBody.checkId);

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

webapp.Run();
return;

static TimeSpan retryDelay(int       attempt) => TimeSpan.FromMinutes(attempt * attempt);
static bool isRetryAllowed(Exception exception) => exception is not (OutOfMemoryException or PagerDutyException { RetryAllowedAfterDelay: false });

internal delegate IPagerDuty PagerDutyFactory(string integrationKey);