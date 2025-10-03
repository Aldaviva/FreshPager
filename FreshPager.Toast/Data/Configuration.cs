namespace FreshPager.Toast.Data;

public class Configuration {

    public required Uri hubAddress { get; init; }
    public required IReadOnlyDictionary<string, PagerDutyAccount> pagerDutyAccountsBySubdomain { get; init; }

}

public record PagerDutyAccount(string apiAccessKey, string userId, string userEmailAddress);