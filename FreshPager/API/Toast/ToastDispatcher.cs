using FreshPager.Common.Events;
using Microsoft.AspNetCore.SignalR;
using Pager.Duty.Webhooks.Requests;

namespace FreshPager.API.Toast;

public interface ToastDispatcher: EventsFromHub;

public class ToastDispatcherImpl(IHubContext<ToastHub, EventsFromHub> toastHub): ToastDispatcher {

    public Task incidentUpdated(IncidentWebhookPayload incident) => toastHub.Clients.All.incidentUpdated(incident);

}