namespace FreshPager.Data;

public class Configuration {

    public required IReadOnlyDictionary<int, string> pagerDutyIntegrationKeysByFreshpingCheckId { get; init; }
    public IReadOnlyList<string>? pagerDutyWebhookSecrets { get; init; }
    public string? alarmLightUrl { get; init; }

}