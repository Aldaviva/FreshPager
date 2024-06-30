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
    .AddHttpClient();

await using WebApplication webapp = builder.Build();

int serviceCount = webapp.Services.GetRequiredService<IOptions<Configuration>>().Value.pagerDutyIntegrationKeysByService.Keys.Count;
var dedupKeys    = new ConcurrentDictionary<string, string>(Math.Min(serviceCount * 2, Environment.ProcessorCount), serviceCount);

webapp.MapPost("/", async Task<IResult> ([FromBody] WebhookPayload payload, IOptions<Configuration> configuration, HttpClient http, ILogger<Program> logger) => {
    string serviceName = payload.checkName;
    logger.LogTrace("Received webhook payload {payload}", payload);

    if (configuration.Value.pagerDutyIntegrationKeysByService.TryGetValue(serviceName, out string? pagerDutyIntegrationKey)) {
        using IPagerDuty pagerDuty = new PagerDuty(pagerDutyIntegrationKey) { HttpClient = http };

        if (payload.isServiceUp) {
            logger.LogDebug("{service} is available", serviceName);
            if (dedupKeys.TryRemove(pagerDutyIntegrationKey, out string? dedupKey)) {
                await pagerDuty.Send(new ResolveAlert(dedupKey));
                logger.LogInformation("Resolved PagerDuty alert for {service} being up, using deduplication key {key}", serviceName, dedupKey);
            } else {
                logger.LogWarning("No known PagerDuty alerts for service {service}, not resolving anything", serviceName);
            }
        } else {
            const uint MAX_TRIGGER_ATTEMPTS = 20;
            logger.LogDebug("{service} is down", serviceName);
            dedupKeys.TryGetValue(pagerDutyIntegrationKey, out string? oldDedupKey);
            Uri reportUrl = new UriBuilder("https", $"{payload.organizationSubdomain}.freshping.io", -1, "reports").Uri.WithQueryParameter("check_id", payload.checkId);

            try {
                string newDedupKey = await Retrier.Attempt(async _ => {
                    AlertResponse alertResponse = await pagerDuty.Send(new TriggerAlert(Severity.Error, payload.eventTitle) {
                        DedupKey  = oldDedupKey,
                        Timestamp = payload.requestDateTime,
                        Links = {
                            new Link(reportUrl, "Freshping Report"),
                            new Link(payload.checkedUrl, "Checked Service URL")
                        },
                        CustomDetails = new Dictionary<string, object> {
                            { "Check", payload.checkName },
                            { "Request Location", payload.requestLocation },
                            { "Response Duration (sec)", payload.responseTime.TotalSeconds },
                            { "Response Status Code", payload.responseStatusCode?.ToString() ?? "(none)" },
                            { "Service State", payload.responseState },
                            { "Service Summary", payload.responseSummary }
                        },

                        // The following fields do not appear in the Android app UI
                        Class     = payload.responseSummary,
                        Component = payload.checkedUrl.ToString(),
                        Source    = payload.requestLocation
                    });

                    dedupKeys[pagerDutyIntegrationKey] = alertResponse.DedupKey;
                    return alertResponse.DedupKey;
                }, MAX_TRIGGER_ATTEMPTS, n => TimeSpan.FromMinutes(n * n), exception => exception is not (OutOfMemoryException or PagerDutyException { RetryAllowedAfterDelay: false }));

                logger.LogInformation("Triggered alert in PagerDuty for {service} being down, got deduplication key {key}", serviceName, newDedupKey);
            } catch (Exception e) when (e is not OutOfMemoryException) {
                logger.LogError(e, "Failed to trigger alert in PagerDuty after {attempts}, giving up", MAX_TRIGGER_ATTEMPTS);
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, detail: "Failed to trigger PagerDuty alert");
            }
        }

        return Results.Created();
    } else {
        logger.LogWarning("No PagerDuty integration key configured for Freshping service {service}, not sending an alert to PagerDuty", serviceName);
        return Results.NotFound(serviceName);
    }
});

await webapp.RunAsync();