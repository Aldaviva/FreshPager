using FreshPager.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Pager.Duty;
using Pager.Duty.Exceptions;
using Pager.Duty.Requests;
using Pager.Duty.Responses;
using System.Collections.Concurrent;
using ThrottleDebounce.Retry;
using Unfucked;

namespace FreshPager.API;

public class FreshpingResource: WebResource {

    private static readonly RetryOptions RETRY_OPTIONS = new() {
        MaxAttempts    = 20,
        Delay          = Delays.Exponential(TimeSpan.FromSeconds(5), max: TimeSpan.FromMinutes(5)),
        IsRetryAllowed = (exception, _) => exception is not (OutOfMemoryException or PagerDutyException { RetryAllowedAfterDelay: false })
    };

    private readonly  ILogger<FreshpingResource>                   logger;
    private readonly  IOptions<Configuration>                      configuration;
    internal readonly ConcurrentDictionary<FreshpingCheck, string> dedupKeys;

    public FreshpingResource(ILogger<FreshpingResource> logger, IOptions<Configuration> configuration) {
        this.logger        = logger;
        this.configuration = configuration;
        int checkCount = configuration.Value.pagerDutyIntegrationKeysByFreshpingCheckId.Count;
        dedupKeys = new ConcurrentDictionary<FreshpingCheck, string>(Math.Min(checkCount * 2, Environment.ProcessorCount), checkCount);
    }

    public void map(WebApplication webapp) {
        webapp.MapPost("/freshping", async Task<IResult> ([FromBody] FreshpingWebhookPayload payload, PagerDutyFactory pagerDutyFactory) => {
            FreshpingCheck check = payload.check;
            logger.LogTrace("Received webhook payload from Freshping: {payload}", payload);

            if (configuration.Value.pagerDutyIntegrationKeysByFreshpingCheckId.TryGetValue(check.id, out string? integrationKey)) {
                using IPagerDuty pagerDuty = pagerDutyFactory(integrationKey);

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
        logger.LogDebug("Listening for Freshping webhooks");
    }

    private async Task<IResult> onCheckDown(FreshpingCheck check, IPagerDuty pagerDuty, FreshpingWebhookPayload requestBody) {
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
            }, RETRY_OPTIONS);

            logger.LogInformation("Triggered alert in PagerDuty for {check} being down, got deduplication key {key}", check.name, newDedupKey);
        } catch (Exception e) when (e is not OutOfMemoryException) {
            logger.LogError(e, "Failed to trigger alert in PagerDuty after {attempts}, giving up", RETRY_OPTIONS.MaxAttempts);
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, detail: "Failed to trigger PagerDuty alert");
        }
        return Results.Created();
    }

    private async Task<IResult> onCheckUp(FreshpingCheck check, IPagerDuty pagerDuty) {
        logger.LogInformation("Freshping reports that {check} is up", check.name);
        if (dedupKeys.TryRemove(check, out string? dedupKey)) {
            ResolveAlert resolution = new(dedupKey);
            await Retrier.Attempt(async _ => await pagerDuty.Send(resolution), RETRY_OPTIONS);
            logger.LogInformation("Resolved PagerDuty alert for {check} being down using deduplication key {key}", check.name, dedupKey);
        } else {
            logger.LogWarning("No known PagerDuty alerts for check {check}, not resolving anything", check.name);
        }
        return Results.NoContent();
    }

}