using FreshPager.Toast.Data;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;
using Unfucked.HTTP;
using Unfucked.HTTP.Config;

namespace FreshPager.Toast.PagerDuty;

public interface PagerDutyRestClientFactory {

    IWebTarget createPagerDutyClient(PagerDutyAccount account);

}

/*
 * https://developer.pagerduty.com/docs/rest-api-overview
 * https://developer.pagerduty.com/docs/authentication
 */
public class PagerDutyRestClientFactoryImpl(HttpClient http): PagerDutyRestClientFactory {

    private static readonly Uri PAGERDUTY_API_BASE = new("https://api.pagerduty.com");

    private static readonly JsonSerializerOptions JSON_OPTIONS = new(JsonSerializerDefaults.Web) {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters           = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public IWebTarget createPagerDutyClient(PagerDutyAccount account) => http.Target(PAGERDUTY_API_BASE)
        .Property(PropertyKey.JsonSerializerOptions, JSON_OPTIONS)
        .Authorization($"Token token={account.apiAccessKey}")
        .Accept(MediaTypeNames.Application.Json)
        .Header(HttpHeaders.FROM, account.userEmailAddress);

}