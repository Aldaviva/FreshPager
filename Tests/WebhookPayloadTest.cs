using FluentAssertions;
using FreshPager.Data;
using System.Text.Json;

namespace Tests;

public class WebhookPayloadTest {

    [Fact]
    public void deserializeDown() {
        // language=json
        const string PAYLOAD =
            """
            {
                "text": "Aldaviva HTTP (https://aldaviva.com) is DOWN.",
                "check_id": "36897",
                "check_name": "Aldaviva HTTP",
                "check_url": "https://aldaviva.com",
                "request_timeout": "30",
                "request_location": "US East (N. Virginia)",
                "request_datetime": "2024-06-28T18:07:46.709971+00:00",
                "response_status_code": "None",
                "response_summary": "Connection Timeout",
                "response_state": "Not Responding",
                "response_time": "30003",
                "event_data": {
                    "org_name": "aldaviva",
                    "event_created_on": "2024-06-28T18:07:46.710438+00:00",
                    "event_id": 17960760,
                    "org_id": 10593,
                    "webhook_type": "AT",
                    "webhook_id": 35191
                }
            }
            """;

        WebhookPayload actual = JsonSerializer.Deserialize<WebhookPayload>(PAYLOAD)!;

        actual.eventTitle.Should().Be("Aldaviva HTTP (https://aldaviva.com) is DOWN.");
        actual.checkId.Should().Be(36897);
        actual.checkName.Should().Be("Aldaviva HTTP");
        actual.checkedUrl.Should().Be("https://aldaviva.com");
        actual.requestTimeout.Should().Be(TimeSpan.FromSeconds(30));
        actual.requestLocation.Should().Be("US East (N. Virginia)");
        actual.requestDateTime.Should().Be(new DateTimeOffset(2024, 6, 28, 18, 7, 46, 709, 971, TimeSpan.Zero));
        actual.responseStatusCode.Should().BeNull();
        actual.responseSummary.Should().Be("Connection Timeout");
        actual.responseState.Should().Be("Not Responding");
        actual.responseTime.Should().Be(TimeSpan.FromMilliseconds(30003));
        actual.organizationSubdomain.Should().Be("aldaviva");
        actual.eventCreationDateTime.Should().Be(new DateTimeOffset(2024, 6, 28, 18, 7, 46, 710, 438, TimeSpan.Zero));
        actual.eventId.Should().Be(17960760);
        actual.organizationId.Should().Be(10593);
        actual.eventFilter.Should().Be(WebhookPayload.EventFilter.UP_DOWN);
        actual.webhookId.Should().Be(35191);
        actual.isServiceUp.Should().BeFalse();
    }

    [Fact]
    public void deserializeUp() {
        // language=json
        const string PAYLOAD =
            """
            {
                "text": "Aldaviva SMTP (tcp://aldaviva.com:25) is UP.",
                "check_id": "829684",
                "check_name": "Aldaviva SMTP",
                "check_url": "tcp://aldaviva.com:25",
                "request_timeout": "30",
                "request_location": "US East (N. Virginia)",
                "request_datetime": "2024-06-28T18:07:46.709971+00:00",
                "response_status_code": "1",
                "response_summary": "Available",
                "response_state": "Available",
                "response_time": "17",
                "event_data": {
                    "org_name": "aldaviva",
                    "event_created_on": "2024-06-28T18:07:46.710438+00:00",
                    "event_id": 17960894,
                    "org_id": 10593,
                    "webhook_type": "AT",
                    "webhook_id": 35191
                }
            }
            """;

        WebhookPayload actual = JsonSerializer.Deserialize<WebhookPayload>(PAYLOAD)!;

        actual.eventTitle.Should().Be("Aldaviva SMTP (tcp://aldaviva.com:25) is UP.");
        actual.checkId.Should().Be(829684);
        actual.checkName.Should().Be("Aldaviva SMTP");
        actual.checkedUrl.Should().Be("tcp://aldaviva.com:25");
        actual.requestTimeout.Should().Be(TimeSpan.FromSeconds(30));
        actual.requestLocation.Should().Be("US East (N. Virginia)");
        actual.requestDateTime.Should().Be(new DateTimeOffset(2024, 6, 28, 18, 7, 46, 709, 971, TimeSpan.Zero));
        actual.responseStatusCode.Should().Be(1);
        actual.responseSummary.Should().Be("Available");
        actual.responseState.Should().Be("Available");
        actual.responseTime.Should().Be(TimeSpan.FromMilliseconds(17));
        actual.organizationSubdomain.Should().Be("aldaviva");
        actual.eventCreationDateTime.Should().Be(new DateTimeOffset(2024, 6, 28, 18, 7, 46, 710, 438, TimeSpan.Zero));
        actual.eventId.Should().Be(17960894);
        actual.organizationId.Should().Be(10593);
        actual.eventFilter.Should().Be(WebhookPayload.EventFilter.UP_DOWN);
        actual.webhookId.Should().Be(35191);
        actual.isServiceUp.Should().BeTrue();
    }

}