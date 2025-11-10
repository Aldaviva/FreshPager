using FreshPager.Common.Events;
using Microsoft.AspNetCore.SignalR;
using System.Net;

namespace FreshPager.API.Toast;

public class ToastHub(ILogger<ToastHub> logger): Hub<EventsFromHub> {

    private static readonly IReadOnlyList<IPNetwork2> CLIENT_IP_ADDRESS_WHITELIST = [
        ..IPNetwork2.ParseRange("::ffff:192.168.1.2-::ffff:192.168.1.254"),
        ..IPNetwork2.ParseRange("192.168.1.2-192.168.1.254")
    ];

    public override async Task OnConnectedAsync() {
        IPAddress clientIpAddress = Context.GetHttpContext()!.Connection.RemoteIpAddress!;

        if (!CLIENT_IP_ADDRESS_WHITELIST.Any(allowedRange => allowedRange.Contains(clientIpAddress))) {
            logger.Debug("Disconnected client from disallowed IP address {addr}", clientIpAddress);
            Context.Abort();
        } else {
            await base.OnConnectedAsync();
        }
    }

}