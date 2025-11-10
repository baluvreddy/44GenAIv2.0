using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Text.Json;

namespace MyWPFApp
{
    public class ApiSettings
    {
        public string BaseUrl { get; set; } = "http://127.0.0.1:8002/";
        public string ApiKey { get; set; } = "";
        public int TimeoutSeconds { get; set; } =100;
        public Dictionary<string, string> Endpoints { get; set; } = new();
    }

    public sealed class ApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ApiSettings _settings;

        public ApiClient()
        {
            // Try to load configuration from appsettings.json located next to the executable or in the project output.
            ApiSettings loaded = null;
            try
            {
                var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Api", out var apiSection))
                    {
                        loaded = JsonSerializer.Deserialize<ApiSettings>(apiSection.GetRawText());
                    }
                }
            }
            catch
            {
                // Ignore and fall back to defaults
            }

            _settings = loaded ?? new ApiSettings();

            // Normalize endpoints to a case-insensitive dictionary so callers can use either "Login" or "login"
            if (_settings.Endpoints == null)
            {
                _settings.Endpoints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in _settings.Endpoints)
                {
                    if (!normalized.ContainsKey(kv.Key))
                        normalized[kv.Key] = kv.Value;
                }
                _settings.Endpoints = normalized;
            }

            // FALLBACK: if no endpoints were loaded from appsettings.json, populate sensible defaults
            if (_settings.Endpoints.Count == 0)
            {
                _settings.Endpoints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["GetTestSteps"] = "testcases/{testCaseId}/steps",
                    ["GetTestCases"] = "projects/{projectId}/testcases",
                    ["GetProjects"] = "my-projects",
                    ["CreateProject"] = "project/",
                    ["Login"] = "login/",
                    ["TestPlan"] = "testplan/{testCaseId}",
                    ["ExecuteCode"] = "execute-code?script_type={script_type}",
                    ["ExecutionLogs"] = "execution"
                };
            }

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_settings.BaseUrl),
                Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds)
            };

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("accept", "application/json");
            if (!string.IsNullOrEmpty(_settings.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _settings.ApiKey);
            }
        }

        // Expose configured base URL (as string) so other components can build WebSocket URIs
        public string BaseUrl => _httpClient.BaseAddress?.ToString() ?? _settings.BaseUrl;

        public void SetBearer(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = null;
                return;
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Resolve a configured key or a template string. Parameters replace {name} placeholders.
        public string Resolve(string keyOrTemplate, Dictionary<string, string>? parameters = null)
        {
            string template = keyOrTemplate;
            if (_settings.Endpoints != null && _settings.Endpoints.TryGetValue(keyOrTemplate, out var configured))
            {
                template = configured;
            }

            if (parameters != null)
            {
                foreach (var kv in parameters)
                {
                    template = template.Replace("{" + kv.Key + "}", Uri.EscapeDataString(kv.Value ?? ""));
                }
            }

            return template;
        }

        public System.Threading.Tasks.Task<HttpResponseMessage> GetAsync(string keyOrTemplate, Dictionary<string, string>? parameters = null) =>
            _httpClient.GetAsync(Resolve(keyOrTemplate, parameters));

        public System.Threading.Tasks.Task<HttpResponseMessage> PostAsync(string keyOrTemplate, HttpContent content, Dictionary<string, string>? parameters = null) =>
            _httpClient.PostAsync(Resolve(keyOrTemplate, parameters), content);

        public void Dispose() => _httpClient?.Dispose();
    }
}