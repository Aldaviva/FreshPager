namespace FreshPager.Data;

public class Configuration {

    private const string ALARM_LIGHT_PAGER_DUTY_SUBDOMAINS_PLACEHOLDER =
        "<Subdomain (such as 'myorg') of PagerDuty organization whose push notifications should trigger the alarm light, or empty array to allow all organizations>";

    public required IReadOnlyDictionary<int, string> pagerDutyIntegrationKeysByFreshpingCheckId { get; init; }
    public IReadOnlyList<string>? pagerDutyWebhookSecrets { get; init; }
    public string? alarmLightUrl { get; init; }

    public IReadOnlySet<string> alarmLightPagerDutySubdomains {
        get;
        private init => field = new HashSet<string>(value.Minus([ALARM_LIGHT_PAGER_DUTY_SUBDOMAINS_PLACEHOLDER]), StringComparer.OrdinalIgnoreCase);
    } = new HashSet<string>(0);

}