using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using TelemetryHil.Core.Services;
using Xunit;

public class HttpSignalCaptureTests
{
    /// <summary>Fake handler returning canned responses per path.</summary>
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            if (request.Content is not null)
                LastRequestBody = await request.Content.ReadAsStringAsync(ct);
            return respond(request);
        }
    }

    private static HttpSignalCapture Create(FakeHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://saleae.test") });

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    [Fact]
    public async Task CheckHealth_True_WhenServiceOkAndSaleaeConnected()
    {
        var capture = Create(new FakeHandler(_ =>
            Json("""{"status": "ok", "saleae_connected": true}""")));

        (await capture.CheckHealthAsync()).Should().BeTrue();
        capture.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task CheckHealth_False_WhenSaleaeDisconnected()
    {
        var capture = Create(new FakeHandler(_ =>
            Json("""{"status": "ok", "saleae_connected": false}""")));

        (await capture.CheckHealthAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task CheckHealth_False_WhenServiceUnreachable()
    {
        var capture = Create(new FakeHandler(_ => throw new HttpRequestException("refused")));

        (await capture.CheckHealthAsync()).Should().BeFalse();
        capture.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task Capture_PostsSnakeCaseConfig_AndParsesPulseResult()
    {
        var handler = new FakeHandler(req =>
        {
            req.RequestUri!.AbsolutePath.Should().Be("/capture");
            return Json("""
            {
              "channel": 0, "pulse_count": 42,
              "avg_pulse_width_us": 12.5, "min_pulse_width_us": 8.0,
              "max_pulse_width_us": 65.0, "capture_duration_seconds": 2.0
            }
            """);
        });
        var capture = Create(handler);

        var result = await capture.CaptureAsync(2.0, [0], 10_000_000);

        result.PulseCount.Should().Be(42);
        result.AvgPulseWidthUs.Should().Be(12.5);
        result.CaptureDurationSeconds.Should().Be(2.0);

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        body.RootElement.GetProperty("duration_seconds").GetDouble().Should().Be(2.0);
        body.RootElement.GetProperty("digital_channels")[0].GetInt32().Should().Be(0);
        body.RootElement.GetProperty("sample_rate").GetInt32().Should().Be(10_000_000);
    }

    [Fact]
    public async Task Capture_Throws_OnHttpError()
    {
        var capture = Create(new FakeHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var act = () => capture.CaptureAsync(2.0, [0]);
        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
