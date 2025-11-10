using Bom.Squad;
using FreshPager;
using FreshPager.API;
using FreshPager.API.Toast;
using FreshPager.Data;
using Kasa;
using Microsoft.Extensions.Options;
using Pager.Duty;
using Pager.Duty.Webhooks;
using RuntimeUpgrade.Notifier;
using RuntimeUpgrade.Notifier.Data;
using Options = Kasa.Options;

BomSquad.DefuseUtf8Bom();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host
    .UseWindowsService()
    .UseSystemd();

builder.Services
    .Configure<Configuration>(builder.Configuration, options => options.BindNonPublicProperties = true)
    .AddSingleton<PagerDutyFactory>(provider => key => new PagerDuty(key) { HttpClient = provider.GetRequiredService<HttpClient>() })
    .AddSingleton<KasaParameters>(provider =>
        provider.GetRequiredService<IOptions<Configuration>>().Value is
            { alarmLightUrl: not "<TCP URL of Kasa outlet with optional 0-indexed socket number as path, like 'tcp://192.168.1.100/0'>" and { } url }
            ? new Uri(url, UriKind.Absolute) is var alarmLightUrl && alarmLightUrl.Host.HasText()
                ? new KasaParameters(alarmLightUrl.Host, alarmLightUrl.Segments.ElementAtOrDefault(1) is { } socket && int.TryParse(socket.TrimEnd('/'), out int socketId) ? socketId : null)
                : throw new UriFormatException($"{nameof(Configuration.alarmLightUrl)} has an empty hostname, ensure there is a // between the scheme and hostname (like 'tcp://192.168.1.100')")
            : null!)
    .AddSingleton<IKasaOutlet>(provider =>
        provider.GetService<KasaParameters>() is { } kasaParameters && new Options { LoggerFactory = provider.GetService<ILoggerFactory>() } is var kasaOptions ?
            kasaParameters.socketId is not null ? new MultiSocketKasaOutlet(kasaParameters.hostname, kasaOptions) : new KasaOutlet(kasaParameters.hostname, kasaOptions) : null!)
    .AddSingleton<WebhookResource>(provider => provider.GetRequiredService<IOptions<Configuration>>().Value is { pagerDutyWebhookSecrets: not ["<My PagerDuty webhook secret>"] and { } secrets }
        ? new WebhookResource(secrets) : null!)
    .AddHttpClient()
    .AddSingleton<WebResource, FreshpingResource>()
    .AddSingleton<WebResource, PagerDutyResource>()
    .AddSingleton<WebResource, ToastResource>()
    .AddSingleton<ToastDispatcher, ToastDispatcherImpl>()
    .AddSignalR(options => options.DisableImplicitFromServicesParameters = true);

builder.Logging.AmplifyMessageLevels(options => options.Amplify("Microsoft.AspNetCore.SignalR.Internal.DefaultHubDispatcher", LogLevel.Warning, 2, 3, 5, 11, 13, 14, 15, 19, 21, 22, 23, 24));

await using WebApplication webapp = builder.Build();

foreach (WebResource resource in webapp.Services.GetServices<WebResource>()) {
    resource.map(webapp);
}

using RuntimeUpgradeNotifier runtimeUpgrades = new() {
    LoggerFactory   = webapp.Services.GetRequiredService<ILoggerFactory>(),
    RestartStrategy = RestartStrategy.AutoRestartService
};

await webapp.RunAsync();

namespace FreshPager {

    internal delegate IPagerDuty PagerDutyFactory(string integrationKey);

}