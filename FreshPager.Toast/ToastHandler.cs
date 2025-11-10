using FreshPager.Toast.Data;
using FreshPager.Toast.Eventing;
using FreshPager.Toast.PagerDuty;
using Microsoft.Extensions.Options;
using Microsoft.Toolkit.Uwp.Notifications;
using Pager.Duty.Webhooks.Requests;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Unfucked.HTTP.Config;

namespace FreshPager.Toast;

public interface ToastHandler {

    Task onIncidentUpdated(IHubClient sender, IncidentWebhookPayload incident);

    Task onToastInteraction(ToastNotificationActivatedEventArgsCompat e);

}

/*
 * https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/send-local-toast?tabs=desktop
 */
public class ToastHandlerImpl(PagerDutyRestClientFactory pagerDutyClientFactory, IOptions<Configuration> config, ILogger<ToastHandlerImpl> logger): ToastHandler {

    public async Task onIncidentUpdated(IHubClient sender, IncidentWebhookPayload incident) {
        logger.Info("Incident {id} \"{title}\" was {eventType}", incident.Id, incident.Title, incident.EventType);
        string  tag   = incident.Id;
        string? group = incident.Service.Summary;
        switch (incident.EventType) {
            case IncidentEventType.Triggered:
            case IncidentEventType.Unacknowledged:
            case IncidentEventType.Reopened:
            case IncidentEventType.Reassigned:
            case IncidentEventType.Escalated:
                clearOldToastsForIncident();
                PagerDutyAccount? pagerDutyAccount = getPagerDutyAccount(incident);

                if (incident.Assignees.Count == 0 || incident.Assignees.Any(assignee => assignee.Id == pagerDutyAccount?.userId)) {
                    new ToastContentBuilder()
                        .SetToastDuration(ToastDuration.Long)
                        .SetToastScenario(ToastScenario.Alarm)
                        .SetProtocolActivation(incident.HtmlUrl)
                        .AddArgument("incidentId", incident.Id)
                        .AddArgument("incidentWebUrl", incident.HtmlUrl.ToString())
                        .AddAppLogoOverride(await saveLogo(), alternateText: "PagerDuty")
                        .AddText(incident.Service.Summary)
                        .AddText(incident.Title)
                        .AddAttributionText($"#{incident.IncidentNumber} {incident.EventType}")
                        .AddButton(new ToastButton()
                            .SetContent("Acknowledge")
                            .AddArgument("action", ButtonAction.ACKNOWLEDGE)
                            .SetBackgroundActivation())
                        .AddButton(new ToastButton()
                            .SetContent("Resolve")
                            .AddArgument("action", ButtonAction.RESOLVE)
                            .SetBackgroundActivation())
                        .Show(toast => {
                            toast.Tag   = tag;
                            toast.Group = group;
                        });
                    logger.Debug("Showed toast for untriaged incident");
                }
                break;
            case IncidentEventType.Acknowledged:
            case IncidentEventType.Resolved:
                clearOldToastsForIncident();
                logger.Debug("Removed toast for triaged incident");
                break;
            default:
                break;
        }

        void clearOldToastsForIncident() => ToastNotificationManagerCompat.History.Remove(tag, group);
    }

    /*
     * https://developer.pagerduty.com/api-reference/8a0e1aa2ec666-update-an-incident
     */
    public async Task onToastInteraction(ToastNotificationActivatedEventArgsCompat e) {
        ToastArguments args           = ToastArguments.Parse(e.Argument);
        string         incidentId     = args.Get("incidentId");
        Uri            incidentWebUrl = new(args.Get("incidentWebUrl"));
        ButtonAction   action         = args.GetEnum<ButtonAction>("action");

        if (getPagerDutyAccount(incidentWebUrl) is not { } pagerDutyAccount) return;

        IncidentStatus newStatus = action switch {
            ButtonAction.ACKNOWLEDGE => IncidentStatus.Acknowledged,
            ButtonAction.RESOLVE     => IncidentStatus.Resolved
        };
        IncidentPayload requestBody = new(new IncidentUpdate(newStatus));

        if (pagerDutyClientFactory.createPagerDutyClient(pagerDutyAccount) is { } client) {
            logger.Info("Setting incident {id} to {newStatus}", incidentId, newStatus);
            using HttpResponseMessage _ = await client.Path("incidents/{id}")
                .ResolveTemplate("id", incidentId)
                .Put(JsonContent.Create(requestBody, options: client.Property(PropertyKey.JsonSerializerOptions, out JsonSerializerOptions? jsonOptions) ? jsonOptions : null));
        }
    }

    private PagerDutyAccount? getPagerDutyAccount(IncidentWebhookPayload incident) => getPagerDutyAccount(incident.HtmlUrl);

    private PagerDutyAccount? getPagerDutyAccount(Uri incidentWebUrl) {
        string            subdomain = incidentWebUrl.Host[..incidentWebUrl.Host.LastIndexOf(".pagerduty.", StringComparison.Ordinal)];
        PagerDutyAccount? account   = config.Value.pagerDutyAccountsBySubdomain.GetValueOrDefault(subdomain);
        if (account == null) {
            logger.Warn("No configured integration key for PagerDuty subdomain {subdomain}, ignoring update to incident {url}", subdomain, incidentWebUrl);
        }
        return account;
    }

    private static async Task<Uri> saveLogo() {
        string logoPath = Path.Combine(Path.GetTempPath(), "FreshPager.png");
        try {
            await using FileStream logoWriteStream = new(logoPath, FileMode.CreateNew, FileAccess.Write); // only write if file is missing
            await using Stream     logoReadStream  = Assembly.GetExecutingAssembly().GetManifestResourceStream("FreshPager.Toast.pagerduty.png")!;
            await logoReadStream.CopyToAsync(logoWriteStream);
        } catch (Exception) {
            /* leave the file nonexistent */
        }
        return new Uri(new Uri("file://"), logoPath);
    }

    private enum ButtonAction {

        ACKNOWLEDGE = 0,
        RESOLVE     = 1

    }

}