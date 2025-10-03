using FreshPager.Common.Events;
using SignalRClientGenerator;

namespace FreshPager.Toast.Eventing;

[GenerateSignalRClient(incoming: [typeof(EventsFromHub)], outgoing: [])]
public partial class HubClient;