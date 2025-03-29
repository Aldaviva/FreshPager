using Bom.Squad;
using FreshPager;
using FreshPager.Data;
using Kasa;
using Microsoft.Extensions.Options;
using Pager.Duty;
using Pager.Duty.Webhooks;
using System.Collections.Concurrent;
using Options = Kasa.Options;

BomSquad.DefuseUtf8Bom();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host
    .UseWindowsService()
    .UseSystemd();

builder.Services
    .Configure<Configuration>(builder.Configuration)
    .AddSingleton<PagerDutyFactory>(provider => key => new PagerDuty(key) { HttpClient = provider.GetRequiredService<HttpClient>() })
    .AddSingleton(provider => {
        int checkCount = provider.GetRequiredService<IOptions<Configuration>>().Value.pagerDutyIntegrationKeysByFreshpingCheckId.Count;
        return new ConcurrentDictionary<Check, string>(Math.Min(checkCount * 2, Environment.ProcessorCount), checkCount);
    })
    .AddSingleton<KasaParameters>(provider => provider.GetRequiredService<IOptions<Configuration>>().Value is
            { alarmLightUrl: not "<TCP URL of Kasa outlet with optional 0-indexed socket number as path, e.g. 'tcp://192.168.1.100/0'>" and { } url }
        && Uri.TryCreate(url, UriKind.Absolute, out Uri? alarmLightUrl) ? new KasaParameters(alarmLightUrl.Host,
            alarmLightUrl.Segments.ElementAtOrDefault(1) is { } socket && int.TryParse(socket.TrimEnd('/'), out int socketId) ? socketId : null) : null!)
    .AddSingleton<IKasaOutlet>(provider => {
        if (provider.GetService<KasaParameters>() is { } kasaParameters) {
            Options kasaOptions = new() { LoggerFactory = provider.GetService<ILoggerFactory>() };
            return kasaParameters.socketId is not null
                ? new MultiSocketKasaOutlet(kasaParameters.hostname, kasaOptions)
                : new KasaOutlet(kasaParameters.hostname, kasaOptions);
        } else {
            return null!;
        }
    })
    .AddSingleton<WebhookResource>(provider => provider.GetRequiredService<IOptions<Configuration>>().Value is { pagerDutyWebhookSecrets: not ["<My PagerDuty webhook secret>"] and { } secrets }
        ? new WebhookResource(secrets) : null!)
    .AddSingleton<WebResource, FreshpingResource>()
    .AddSingleton<WebResource, PagerDutyResource>()
    .AddHttpClient();

await using WebApplication webapp = builder.Build();

foreach (WebResource resource in webapp.Services.GetServices<WebResource>()) {
    resource.map(webapp);
}

await webapp.RunAsync();

internal delegate IPagerDuty PagerDutyFactory(string integrationKey);