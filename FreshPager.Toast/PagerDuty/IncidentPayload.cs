using Pager.Duty.Webhooks.Requests;

namespace FreshPager.Toast.PagerDuty;

public record IncidentPayload(IncidentUpdate incident);

public record IncidentUpdate(IncidentStatus status) {

    public ReferenceType type => ReferenceType.IncidentReference;

}