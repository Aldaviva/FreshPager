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

BomSquad.DefuseUtf8Bom();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host
    .UseWindowsService()
    .UseSystemd();

builder.WebHost.UseKestrel((context, options) => options.ListenAnyIP(context.Configuration.GetValue(nameof(Configuration.httpServerPort), new Configuration().httpServerPort)));

builder.Services
    .Configure<Configuration>(builder.Configuration)
    .AddSingleton<Func<string, IPagerDuty>>(provider => key => new PagerDuty(key) { HttpClient = provider.GetRequiredService<HttpClient>() })
    .AddSingleton(provider => {
        int checkCount = provider.GetRequiredService<IOptions<Configuration>>().Value.pagerDutyIntegrationKeysByService.Keys.Count;
        return new ConcurrentDictionary<string, string>(Math.Min(checkCount * 2, Environment.ProcessorCount), checkCount);
    })
    .AddHttpClient();

await using WebApplication webapp = builder.Build();

var logger    = webapp.Services.GetRequiredService<ILogger<Program>>();
var dedupKeys = webapp.Services.GetRequiredService<ConcurrentDictionary<string, string>>();

webapp.MapPost("/", async Task<IResult> ([FromBody] WebhookPayload payload, Func<string, IPagerDuty> pagerDutyFactory, IOptions<Configuration> configuration) => {
    string serviceName = payload.checkName;
    logger.LogTrace("Received webhook payload {payload}", payload);

    if (configuration.Value.pagerDutyIntegrationKeysByService.TryGetValue(serviceName, out string? pagerDutyIntegrationKey)) {
        using IPagerDuty pagerDuty = pagerDutyFactory(pagerDutyIntegrationKey);

        if (payload.isServiceUp) {
            return await onServiceUp(serviceName, pagerDutyIntegrationKey, pagerDuty);
        } else {
            return await onServiceDown(payload, serviceName, pagerDutyIntegrationKey, pagerDuty);
        }
    } else {
        logger.LogWarning("No PagerDuty integration key configured for Freshping service {service}, not sending an alert to PagerDuty", serviceName);
        return Results.NoContent();
    }
});

async Task<IResult> onServiceUp(string serviceName, string pagerDutyIntegrationKey, IPagerDuty pagerDuty) {
    logger.LogDebug("{service} is available", serviceName);
    if (dedupKeys.TryRemove(pagerDutyIntegrationKey, out string? dedupKey)) {
        await pagerDuty.Send(new ResolveAlert(dedupKey));
        logger.LogInformation("Resolved PagerDuty alert for {service} being up, using deduplication key {key}", serviceName, dedupKey);
    } else {
        logger.LogWarning("No known PagerDuty alerts for service {service}, not resolving anything", serviceName);
    }
    return Results.NoContent();
}

async Task<IResult> onServiceDown(WebhookPayload requestBody, string serviceName, string pagerDutyIntegrationKey, IPagerDuty pagerDuty) {
    const uint MAX_TRIGGER_ATTEMPTS = 20;
    logger.LogDebug("{service} is down", serviceName);
    dedupKeys.TryGetValue(pagerDutyIntegrationKey, out string? oldDedupKey);
    Uri reportUrl = new UriBuilder("https", $"{requestBody.organizationSubdomain}.freshping.io", -1, "reports").Uri.WithQueryParameter("check_id", requestBody.checkId);

    try {
        string newDedupKey = await Retrier.Attempt(async _ => {
            AlertResponse alertResponse = await pagerDuty.Send(new TriggerAlert(Severity.Error, requestBody.eventTitle) {
                DedupKey  = oldDedupKey,
                Timestamp = requestBody.requestDateTime,
                Links = {
                    new Link(reportUrl, "Freshping Report"),
                    new Link(requestBody.checkedUrl, "Checked Service URL")
                },
                CustomDetails = new Dictionary<string, object> {
                    ["Check"]                   = requestBody.checkName,
                    ["Request Location"]        = requestBody.requestLocation,
                    ["Response Duration (sec)"] = requestBody.responseTime.TotalSeconds,
                    ["Response Status Code"]    = requestBody.responseStatusCode?.ToString() ?? "(none)",
                    ["Service State"]           = requestBody.responseState,
                    ["Service Summary"]         = requestBody.responseSummary
                },

                // The following fields only appear on the webapp alert details page, and nowhere in the mobile app
                Class     = requestBody.responseSummary,
                Component = requestBody.checkedUrl.ToString(),
                Source    = requestBody.requestLocation
            });

            dedupKeys[pagerDutyIntegrationKey] = alertResponse.DedupKey;
            return alertResponse.DedupKey;
        }, MAX_TRIGGER_ATTEMPTS, n => TimeSpan.FromMinutes(n * n), exception => exception is not (OutOfMemoryException or PagerDutyException { RetryAllowedAfterDelay: false }));

        logger.LogInformation("Triggered alert in PagerDuty for {service} being down, got deduplication key {key}", serviceName, newDedupKey);
    } catch (Exception e) when (e is not OutOfMemoryException) {
        logger.LogError(e, "Failed to trigger alert in PagerDuty after {attempts}, giving up", MAX_TRIGGER_ATTEMPTS);
        return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, detail: "Failed to trigger PagerDuty alert");
    }
    return Results.Created();
}

await webapp.RunAsync();