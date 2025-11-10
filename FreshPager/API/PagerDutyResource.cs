using FreshPager.API.Toast;
using FreshPager.Data;
using Kasa;
using Microsoft.Extensions.Options;
using Pager.Duty.Webhooks;
using Pager.Duty.Webhooks.Requests;
using System.Collections.Concurrent;
using ThrottleDebounce;

namespace FreshPager.API;

public class PagerDutyResource(
    WebhookResource? webhookResource,
    IKasaOutlet? kasa,
    KasaParameters? kasaParameters,
    ToastDispatcher toasts,
    IOptions<Configuration> config,
    ILogger<PagerDutyResource> logger
): WebResource {

    private readonly ConcurrentDictionary<Uri, IncidentStatus> allIncidentStatuses = new();

    private Func<bool, Task?> debouncedSetSocketOn = _ => Task.CompletedTask;

    public void map(WebApplication webapp) {
        RequestDelegate webhookHandler;
        if (webhookResource != null && kasa != null) {
            webhookHandler       = webhookResource.HandlePostRequest;
            debouncedSetSocketOn = Debouncer.Debounce<bool, Task>(setSocketOn, TimeSpan.FromSeconds(1)).Invoke;

            webhookResource.IncidentReceived += async (_, incident) => await onIncidentReceived(incident);
            webhookResource.PingReceived     += (_, _) => logger.Info("Test webhook event received from PagerDuty");
        } else {
            webhookHandler = context => {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return Task.CompletedTask;
            };
        }
        webapp.MapPost("/pagerduty", webhookHandler);
        logger.Debug("Listening for PagerDuty webhooks");
    }

    private async Task onIncidentReceived(IncidentWebhookPayload incident) {
        if (incident.EventType is IncidentEventType.Triggered or IncidentEventType.Acknowledged or IncidentEventType.Unacknowledged or IncidentEventType.Resolved or IncidentEventType.Reopened) {
            allIncidentStatuses[incident.HtmlUrl] = incident.Status;
            bool isTriggered                  = incident.Status == IncidentStatus.Triggered;
            Uri? otherTriggeredIncidentWebUrl = isTriggered ? null : allIncidentStatuses.ToArray().FirstOrNull(entry => entry.Value == IncidentStatus.Triggered)?.Key ?? null;
            bool turnOn                       = isTriggered || otherTriggeredIncidentWebUrl != null;

            if (isTriggered || !turnOn) {
                logger.Info("PagerDuty incident #{num:D} \"{title}\" is {status}, turning {onOff} {outlet}", incident.IncidentNumber, incident.Title, incident.Status,
                    turnOn ? "on" : "off", kasa!.Hostname);
            } else {
                logger.Info("PagerDuty incident #{num:D} \"{title}\" is {status}, but leaving {outlet} on because the other incident {otherUrl} is still triggered",
                    incident.IncidentNumber, incident.Title, incident.Status, kasa!.Hostname, otherTriggeredIncidentWebUrl);
            }

            string               organizationSubdomain = incident.Self.Host.TrimEnd(".eu.pagerduty.com", ".pagerduty.com");
            IReadOnlySet<string> subdomainWhitelist    = config.Value.alarmLightPagerDutySubdomains;
            if (subdomainWhitelist.Count == 0 || subdomainWhitelist.Contains(organizationSubdomain)) {
                await (debouncedSetSocketOn(turnOn) ?? Task.CompletedTask);
            }

            await toasts.incidentUpdated(incident);

            if (incident.Status == IncidentStatus.Resolved) {
                allIncidentStatuses.TryRemove(new KeyValuePair<Uri, IncidentStatus>(incident.HtmlUrl, incident.Status));
            }
        }
    }

    private async Task setSocketOn(bool turnOn) {
        try {
            if (kasa is IMultiSocketKasaOutlet multiOutlet) {
                await multiOutlet.System.SetSocketOn(kasaParameters!.socketId ?? 0, turnOn);
            } else {
                await kasa!.System.SetSocketOn(turnOn);
            }
        } catch (KasaException e) {
            logger.Error(e, "Failed to turn {onOff} Kasa outlet {host} in response to a PagerDuty webhook request", turnOn ? "on" : "off", kasa!.Hostname);
        } catch (ArgumentOutOfRangeException e) {
            logger.Error(e, "Failed to turn {onOff} socket {socketId} in Kasa outlet {host} because it does not have a socket with that high of an ID", turnOn ? "on" : "off",
                kasaParameters?.socketId, kasa!.Hostname);
        } catch (Exception e) when (e is not OutOfMemoryException) {
            logger.Critical(e, "Uncaught exception while turning {onOff} Kasa outlet {host}", turnOn ? "on" : "off", kasa!.Hostname);
        }
    }

}