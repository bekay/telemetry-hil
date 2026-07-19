using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TelemetryHil.Core.Interfaces;
using TelemetryHil.Core.Models;

namespace TelemetryHil.Core.Services
{
    /// <summary>
    /// Real ISignalCapture against the Saleae FastAPI wrapper
    /// (services/saleae-service, uvicorn on :8000).
    /// </summary>
    public class HttpSignalCapture : ISignalCapture, IDisposable
    {
        public const string DefaultBaseUrl = "http://localhost:8000";

        private readonly HttpClient _http;

        public bool IsConnected { get; private set; }

        public HttpSignalCapture(string baseUrl = DefaultBaseUrl)
            : this(new HttpClient { BaseAddress = new Uri(baseUrl) }) { }

        /// <summary>Test seam — inject a preconfigured HttpClient (fake handler).</summary>
        public HttpSignalCapture(HttpClient http)
        {
            _http = http;
            if (_http.Timeout == TimeSpan.FromSeconds(100))
                _http.Timeout = TimeSpan.FromMinutes(5); // captures can be long
        }

        public async Task<bool> CheckHealthAsync(CancellationToken ct = default)
        {
            try
            {
                var health = await _http.GetFromJsonAsync<HealthDto>("/health", ct);
                IsConnected = health is { Status: "ok", SaleaeConnected: true };
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                IsConnected = false;
            }
            return IsConnected;
        }

        public async Task<CaptureResult> CaptureAsync(
            double durationSeconds,
            int[] digitalChannels,
            int sampleRate = 10_000_000,
            CancellationToken ct = default)
        {
            // Buffered StringContent (not PostAsJsonAsync) so the request has a
            // Content-Length instead of chunked transfer-encoding — maximally
            // compatible with simple HTTP servers.
            var json = System.Text.Json.JsonSerializer.Serialize(new CaptureConfigDto
            {
                DurationSeconds = durationSeconds,
                DigitalChannels = digitalChannels,
                SampleRate = sampleRate,
            });
            var response = await _http.PostAsync("/capture",
                new StringContent(json, System.Text.Encoding.UTF8, "application/json"), ct);
            response.EnsureSuccessStatusCode();

            var dto = await response.Content.ReadFromJsonAsync<PulseResultDto>(ct)
                      ?? throw new InvalidOperationException("empty /capture response");

            return new CaptureResult(
                Channel: dto.Channel,
                PulseCount: dto.PulseCount,
                AvgPulseWidthUs: dto.AvgPulseWidthUs,
                MinPulseWidthUs: dto.MinPulseWidthUs,
                MaxPulseWidthUs: dto.MaxPulseWidthUs,
                CaptureDurationSeconds: dto.CaptureDurationSeconds);
        }

        public void Dispose() => _http.Dispose();

        private sealed class HealthDto
        {
            [JsonPropertyName("status")] public string? Status { get; set; }
            [JsonPropertyName("saleae_connected")] public bool SaleaeConnected { get; set; }
        }

        private sealed class CaptureConfigDto
        {
            [JsonPropertyName("duration_seconds")] public double DurationSeconds { get; set; }
            [JsonPropertyName("digital_channels")] public int[] DigitalChannels { get; set; } = [];
            [JsonPropertyName("sample_rate")] public int SampleRate { get; set; }
        }

        private sealed class PulseResultDto
        {
            [JsonPropertyName("channel")] public int Channel { get; set; }
            [JsonPropertyName("pulse_count")] public int PulseCount { get; set; }
            [JsonPropertyName("avg_pulse_width_us")] public double AvgPulseWidthUs { get; set; }
            [JsonPropertyName("min_pulse_width_us")] public double MinPulseWidthUs { get; set; }
            [JsonPropertyName("max_pulse_width_us")] public double MaxPulseWidthUs { get; set; }
            [JsonPropertyName("capture_duration_seconds")] public double CaptureDurationSeconds { get; set; }
        }
    }
}
