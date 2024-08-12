namespace FreshPager.Data;

public class Configuration {

    public IDictionary<string, string> pagerDutyIntegrationKeysByService { get; } = new Dictionary<string, string>();
    public ushort httpServerPort { get; init; } = 37374;

}