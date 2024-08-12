﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pager.Duty;
using Pager.Duty.Requests;
using Pager.Duty.Responses;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Tests;

/*
 * If you get any of the following errors:
 *   - System.ArgumentException : Argument --parentprocessid was not specified.
 *   - System.InvalidOperationException : Can't find 'C:\Users\Ben\Documents\Projects\FreshPager\Tests\bin\Debug\net8.0\testhost.deps.json'. This file is required for functional tests to run properly. There should be a copy of the file on your source project bin folder. If that is not the case, make sure that the property PreserveCompilationContext is set to true on your project file. E.g '<PreserveCompilationContext>true</PreserveCompilationContext>'. For functional tests to work they need to either run from the build output folder or the testhost.deps.json file from your application's output directory must be copied to the folder where the tests are running on. A common cause for this error is having shadow copying enabled when the tests run.
 * then WebApplicationFactory is trying to use the wrong Program class, and you likely need to make Program visible to the Test project using ONE of the following techniques:
 *   - <InternalsVisibleTo Include="Tests" />
 *   - public partial class Program { }
 */
public class ServerTest: IDisposable {

    private static readonly MediaTypeHeaderValue JSON_TYPE = new("application/json");

    private readonly WebApplicationFactory<Program>       webapp;
    private readonly IPagerDuty                           pagerDuty = A.Fake<IPagerDuty>();
    private readonly HttpClient                           testClient;
    private readonly ConcurrentDictionary<string, string> dedupKeys;

    public ServerTest() {
        if (typeof(Program).FullName == "Microsoft.VisualStudio.TestPlatform.TestHost.Program") {
            throw new InvalidOperationException(
                "Wrong Program class! Make sure Program is (partial) public, or internals are visible to test project. See https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests#basic-tests-with-the-default-webapplicationfactory");
        }

        webapp = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => {
                builder.UseTestServer();
                builder.UseEnvironment("Test");
                // builder.UseConfiguration()
                builder.ConfigureAppConfiguration(c => c.AddJsonFile("appsettings.Test.json", false, false));

                builder.ConfigureTestServices(collection => collection
                    .RemoveAll<Func<string, IPagerDuty>>()
                    .AddSingleton<Func<string, IPagerDuty>>(_ => pagerDuty));
            });

        testClient = webapp.CreateClient();
        dedupKeys  = webapp.Services.GetRequiredService<ConcurrentDictionary<string, string>>();
    }

    [Fact]
    public async Task down() {
        Captured<TriggerAlert> captor = A.Captured<TriggerAlert>();
        A.CallTo(() => pagerDuty.Send(captor._)).Returns(new AlertResponse { DedupKey = "abc" });

        const string PAYLOAD =
            """
            {
                "text": "Aldaviva HTTP (https://aldaviva.com) is DOWN.",
                "check_id": "36897",
                "check_name": "Server A",
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

        dedupKeys.IsEmpty.Should().BeTrue("no alerts yet");

        using HttpResponseMessage response = await testClient.PostAsync("/", new StringContent(PAYLOAD, Encoding.UTF8, JSON_TYPE));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        dedupKeys["123"].Should().Be("abc", "stored dedup key for correct integration key");

        A.CallTo(() => pagerDuty.Send(captor._)).MustHaveHappened();
        TriggerAlert actual = captor.GetLastValue();
        actual.Severity.Should().Be(Severity.Error);
        actual.Summary.Should().Be("Aldaviva HTTP (https://aldaviva.com) is DOWN.");
        actual.DedupKey.Should().BeNull();
        actual.Timestamp.Should().BeExactly(new DateTimeOffset(2024, 6, 28, 18, 7, 46, 709, 971, TimeSpan.Zero));
        actual.Links.Should().Contain(new Link("https://aldaviva.freshping.io/reports?check_id=36897", "Freshping Report"));
        actual.Links.Should().Contain(new Link("https://aldaviva.com/", "Checked Service URL"));
        actual.Class.Should().Be("Connection Timeout");
        actual.Component.Should().Be("https://aldaviva.com/");
        actual.Source.Should().Be("US East (N. Virginia)");
        actual.CustomDetails.Should().BeOfType<Dictionary<string, object>>().Which.Should().Equal(new Dictionary<string, object> {
            { "Check", "Server A" },
            { "Request Location", "US East (N. Virginia)" },
            { "Response Duration (sec)", 30.003 },
            { "Response Status Code", "(none)" },
            { "Service State", "Not Responding" },
            { "Service Summary", "Connection Timeout" }
        });
    }

    [Fact]
    public async Task up() {
        dedupKeys["123"] = "abc";
        A.CallTo(() => pagerDuty.Send(A<ResolveAlert>._)).Returns(new AlertResponse { DedupKey = "abc" });

        const string PAYLOAD =
            """
            {
                "text": "Aldaviva SMTP (tcp://aldaviva.com:25) is UP.",
                "check_id": "829684",
                "check_name": "Server A",
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

        using HttpResponseMessage response = await testClient.PostAsync("/", new StringContent(PAYLOAD, Encoding.UTF8, JSON_TYPE));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        dedupKeys.ContainsKey("123").Should().BeFalse("dedup key should have been cleared after service came up");
        A.CallTo(() => pagerDuty.Send(A<ResolveAlert>.That.Matches(alert => alert.DedupKey == "abc"))).MustHaveHappened();
    }

    [Fact]
    public async Task noAlertToResolve() {
        A.CallTo(() => pagerDuty.Send(A<ResolveAlert>._)).Returns(new AlertResponse { DedupKey = "abc" });

        const string PAYLOAD =
            """
            {
                "text": "Aldaviva SMTP (tcp://aldaviva.com:25) is UP.",
                "check_id": "829684",
                "check_name": "Server A",
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

        using HttpResponseMessage response = await testClient.PostAsync("/", new StringContent(PAYLOAD, Encoding.UTF8, JSON_TYPE));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        A.CallTo(() => pagerDuty.Send(A<ResolveAlert>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task unknownService() {
        const string PAYLOAD =
            """
            {
                "text": "Aldaviva HTTP (https://aldaviva.com) is DOWN.",
                "check_id": "36897",
                "check_name": "Server Z",
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

        using HttpResponseMessage response = await testClient.PostAsync("/", new StringContent(PAYLOAD, Encoding.UTF8, JSON_TYPE));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    public void Dispose() {
        webapp.Dispose();
        testClient.Dispose();
        GC.SuppressFinalize(this);
    }

}