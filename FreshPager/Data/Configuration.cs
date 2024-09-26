namespace FreshPager.Data;

public class Configuration {

    public required IReadOnlyDictionary<int, string> pagerDutyIntegrationKeysByFreshpingCheckId { get; init; }

}