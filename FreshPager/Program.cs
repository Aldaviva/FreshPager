using Bom.Squad;
using FreshPager;
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

builder.WebHost.UseKestrel(options => options.ListenAnyIP(37374));

builder.Services
    .Configure<Configuration>(builder.Configuration)
    .AddHttpClient();

WebApplication webapp = builder.Build();

var dedupKeys = new ConcurrentDictionary<string, string>();

webapp.MapPost("/", async Task<IResult> ([FromBody] WebhookPayload payload, IOptions<Configuration> configuration, HttpClient http, ILogger<Program> logger) => {
    string serviceName = payload.checkName;
    logger.LogTrace("Received webhook payload {payload}", payload);

    if (configuration.Value.pagerDutyIntegrationKeysByService.TryGetValue(serviceName, out string? pagerDutyIntegrationKey)) {
        using IPagerDuty pagerDuty = new PagerDuty(pagerDutyIntegrationKey) { HttpClient = http };

        if (payload.responseSummary == "Available") {
            logger.LogDebug("{service} is available", serviceName);
            if (dedupKeys.TryRemove(serviceName, out string? dedupKey)) {
                await pagerDuty.Send(new ResolveAlert(dedupKey));
                logger.LogInformation("Resolved PagerDuty alert with dedupKey {key}", dedupKey);
            } else {
                logger.LogWarning("No known PagerDuty alerts for service {service}, not resolving anything", serviceName);
            }
        } else {
            dedupKeys.TryGetValue(serviceName, out string? oldDedupKey);
            string     reportUrl    = $"https://{payload.eventData.orgName}.freshping.io/reports?check_id={payload.checkId}";
            const uint MAX_ATTEMPTS = 20;

            try {
                string newDedupKey = await Retrier.Attempt(async i => {
                    AlertResponse alertResponse = await pagerDuty.Send(new TriggerAlert(Severity.Error, payload.text) {
                        Class     = payload.responseSummary,
                        Client    = "Freshping",
                        ClientUrl = reportUrl,
                        Component = payload.checkUrl,
                        DedupKey  = oldDedupKey,
                        Links = {
                            new Link(reportUrl, $"{payload.checkName} report on Freshping"),
                            new Link(payload.checkUrl, "Checked service URL")
                        },
                        Images    = { new Image("https://d3h0owdjgzys62.cloudfront.net/images/7876/live_cover_art/thumb2x/freshping_400.png", null, "Freshping") },
                        Source    = payload.requestLocation,
                        Timestamp = payload.requestDatetime,
                        CustomDetails = new Dictionary<string, object> {
                            { "statusCode", payload.responseStatusCode },
                            { "state", payload.responseState },
                            { "responseDuration", payload.responseTime }
                        }
                    });

                    if (alertResponse.Status != "success") {
                        throw new ApplicationException($"{alertResponse.Status}: {alertResponse.Message}");
                    }

                    dedupKeys[serviceName] = alertResponse.DedupKey;
                    return alertResponse.DedupKey;
                }, MAX_ATTEMPTS, n => TimeSpan.FromMinutes(n * n), exception => exception is not (OutOfMemoryException or PagerDutyException { RetryAllowedAfterDelay: false }));

                logger.LogInformation("Triggered alert in PagerDuty for {service}, returned dedupKey was {key}", serviceName, newDedupKey);
            } catch (Exception e) when (e is not OutOfMemoryException) {
                logger.LogError(e, "Failed to trigger alert in PagerDuty after {attempts}, giving up", MAX_ATTEMPTS);
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }

        return Results.NoContent();
    } else {
        logger.LogWarning("No PagerDuty integration key found for Freshping service {service}", serviceName);
        return Results.NotFound(serviceName);
    }
});

await webapp.RunAsync();