using Pager.Duty.Webhooks.Requests;

namespace FreshPager.Common.Events;

public interface EventsFromHub {

    Task incidentUpdated(IncidentWebhookPayload incident);

}