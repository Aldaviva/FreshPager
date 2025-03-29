using Kasa;
using Pager.Duty.Webhooks;
using Pager.Duty.Webhooks.Requests;
using System.Collections.Concurrent;
using ThrottleDebounce;
using Unfucked;

namespace FreshPager;

public class PagerDutyResource(WebhookResource? webhookResource, IKasaOutlet? kasa, KasaParameters? kasaParameters, ILogger<PagerDutyResource> logger): WebResource {

    private readonly ConcurrentDictionary<Uri, IncidentStatus> allIncidentStatuses = new();

    private Func<bool, Task?>? debouncedSetSocketOn;

    public void map(WebApplication webapp) {
        RequestDelegate webhookHandler;
        if (webhookResource != null && kasa != null) {
            webhookHandler       = webhookResource.HandlePostRequest;
            debouncedSetSocketOn = Debouncer.Debounce<bool, Task>(setSocketOn, TimeSpan.FromSeconds(1)).Invoke;

            webhookResource.IncidentReceived += async (_, incident) => await onIncidentReceived(incident);
            webhookResource.PingReceived     += (_, _) => logger.LogInformation("Test webhook event received from PagerDuty");
        } else {
            webhookHandler = context => {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return Task.CompletedTask;
            };
        }
        webapp.MapPost("/pagerduty", webhookHandler);
        logger.LogDebug("Listening for PagerDuty webhooks");
    }

    private async Task onIncidentReceived(IncidentWebhookPayload incident) {
        if (incident.EventType is IncidentEventType.Triggered or IncidentEventType.Acknowledged or IncidentEventType.Unacknowledged or IncidentEventType.Resolved
            or IncidentEventType.Reopened) {
            allIncidentStatuses[incident.HtmlUrl] = incident.Status;
            bool isTriggered                  = incident.Status == IncidentStatus.Triggered;
            Uri? otherTriggeredIncidentWebUrl = isTriggered ? null : allIncidentStatuses.ToArray().FirstOrNull(entry => entry.Value == IncidentStatus.Triggered)?.Key ?? null;
            bool turnOn                       = isTriggered || otherTriggeredIncidentWebUrl != null;

            if (isTriggered || !turnOn) {
                logger.LogInformation("PagerDuty incident #{num:D} \"{title}\" is {status}, turning {onOff} {outlet}", incident.IncidentNumber, incident.Title, incident.Status,
                    turnOn ? "on" : "off", kasa!.Hostname);
            } else {
                logger.LogInformation("PagerDuty incident #{num:D} \"{title}\" is {status}, but leaving {outlet} on because the other incident {otherUrl} is still triggered",
                    incident.IncidentNumber, incident.Title, incident.Status, kasa!.Hostname, otherTriggeredIncidentWebUrl);
            }

            await (debouncedSetSocketOn?.Invoke(turnOn) ?? Task.CompletedTask);

            if (incident.Status == IncidentStatus.Resolved) {
                allIncidentStatuses.TryRemove(new KeyValuePair<Uri, IncidentStatus>(incident.HtmlUrl, incident.Status));
            }
        }
    }

    private async Task setSocketOn(bool turnOn) {
        try {
            switch (kasa!) {
                case IMultiSocketKasaOutlet multiOutlet:
                    await multiOutlet.System.SetSocketOn(kasaParameters?.socketId ?? 0, turnOn);
                    break;
                default:
                    await kasa!.System.SetSocketOn(turnOn);
                    break;
            }
        } catch (KasaException e) {
            logger.LogError(e, "Failed to turn {onOff} Kasa outlet {host} in response to a PagerDuty webhook request", turnOn ? "on" : "off", kasa!.Hostname);
        } catch (ArgumentOutOfRangeException e) {
            logger.LogError(e, "Failed to turn {onOff} socket {socketId} in Kasa outlet {host} because it does not have a socket with that high of an ID", turnOn ? "on" : "off",
                kasaParameters?.socketId, kasa!.Hostname);
        } catch (Exception e) when (e is not OutOfMemoryException) {
            logger.LogCritical(e, "Uncaught exception while turning {onOff} Kasa outlet {host}", turnOn ? "on" : "off", kasa!.Hostname);
        }
    }

}

public record KasaParameters(string hostname, int? socketId);