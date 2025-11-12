using Pager.Duty.Webhooks.Requests;

namespace FreshPager.Toast;

public static class Extensions {

    public static string ToPhrase(this IncidentEventType incidentEventType) => incidentEventType switch {
        IncidentEventType.Acknowledged        => "acknowledged",
        IncidentEventType.Delegated           => "delegated",
        IncidentEventType.Escalated           => "escalated",
        IncidentEventType.IncidentTypeChanged => "incident type changed",
        IncidentEventType.PriorityUpdated     => "priority updated",
        IncidentEventType.Reassigned          => "reassigned",
        IncidentEventType.Reopened            => "reopened",
        IncidentEventType.Resolved            => "resolved",
        IncidentEventType.ServiceUpdated      => "service updated",
        IncidentEventType.Triggered           => "triggered",
        IncidentEventType.Unacknowledged      => "unacknowledged"
    };

}