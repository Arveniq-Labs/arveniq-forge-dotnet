using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Arveniq.Forge;

/// <summary>Shared transport that keeps the Forge key on the server.</summary>
public abstract class ForgeServerClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string? _userAgent;
    private readonly TimeSpan _requestTimeout;

    protected ForgeServerClient(ForgeClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        BaseUri = NormalizeBaseUrl(options.BaseUrl);
        _apiKey = NormalizeApiKey(options.ApiKey);
        _httpClient = options.HttpClient ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
        _userAgent = string.IsNullOrWhiteSpace(options.UserAgent) ? null : options.UserAgent.Trim();
        _requestTimeout = options.RequestTimeout > TimeSpan.Zero ? options.RequestTimeout : throw new ArgumentOutOfRangeException(nameof(options.RequestTimeout));
    }

    protected Uri BaseUri { get; }

    protected async Task<JsonObject> SendJsonAsync(string path, HttpMethod method, JsonNode? body = null, ForgeRequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        var requestId = RequestId(options?.RequestId);
        using var timeoutSource = CreateTimeoutSource(options?.Timeout, cancellationToken);
        using var request = CreateRequest(path, method, body, "application/json", null, requestId);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutSource.Token).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(timeoutSource.Token).ConfigureAwait(false);
        EnsureSuccess(response, text, requestId);
        return string.IsNullOrWhiteSpace(text) ? new JsonObject() : JsonHelpers.ParseObject(text);
    }

    protected async Task<HttpResponseMessage> SendStreamAsync(string path, HttpMethod method, JsonNode? body, string? lastEventId, ForgeChatStreamOptions options, CancellationToken cancellationToken)
    {
        var requestId = RequestId(options.RequestId);
        using var timeoutSource = CreateTimeoutSource(options.Timeout, cancellationToken);
        var request = CreateRequest(path, method, body, "text/event-stream", lastEventId, requestId);
        try
        {
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync(timeoutSource.Token).ConfigureAwait(false);
                var error = new ForgeApiException($"Forge API request failed with {(int)response.StatusCode}.", (int)response.StatusCode, text, response.Headers.TryGetValues("x-request-id", out var values) ? values.FirstOrDefault() : requestId);
                response.Dispose();
                throw error;
            }
            return response;
        }
        catch
        {
            request.Dispose();
            throw;
        }
    }

    protected static string RequiredId(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{name} is required.", name);
        return Uri.EscapeDataString(value.Trim());
    }

    protected static string RequiredRawId(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{name} is required.", name);
        return value.Trim();
    }

    private HttpRequestMessage CreateRequest(string path, HttpMethod method, JsonNode? body, string accept, string? lastEventId, string requestId)
    {
        if (!path.StartsWith("/", StringComparison.Ordinal)) throw new ArgumentException("Forge API paths must start with '/'.", nameof(path));
        var request = new HttpRequestMessage(method, new Uri(BaseUri, path.TrimStart('/')));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Headers.TryAddWithoutValidation("x-request-id", requestId);
        if (_userAgent is not null) request.Headers.TryAddWithoutValidation("user-agent", _userAgent);
        if (!string.IsNullOrWhiteSpace(lastEventId)) request.Headers.TryAddWithoutValidation("last-event-id", lastEventId);
        if (body is not null) request.Content = new StringContent(body.ToJsonString(JsonHelpers.SerializerOptions), Encoding.UTF8, "application/json");
        return request;
    }

    private CancellationTokenSource CreateTimeoutSource(TimeSpan? requestedTimeout, CancellationToken cancellationToken)
    {
        var timeout = requestedTimeout ?? _requestTimeout;
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(requestedTimeout));
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(timeout);
        return source;
    }

    private static void EnsureSuccess(HttpResponseMessage response, string body, string requestId)
    {
        if (response.IsSuccessStatusCode) return;
        throw new ForgeApiException($"Forge API request failed with {(int)response.StatusCode}.", (int)response.StatusCode, body, response.Headers.TryGetValues("x-request-id", out var values) ? values.FirstOrDefault() : requestId);
    }

    private static Uri NormalizeBaseUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            throw new ArgumentException("baseUrl must be an absolute HTTP(S) URL.", nameof(value));
        if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("baseUrl must not contain credentials, a query string, or a fragment.", nameof(value));
        if (uri.Scheme == "http" && !IsLoopback(uri.Host)) throw new ArgumentException("baseUrl must use HTTPS unless it targets a loopback host.", nameof(value));
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
    }

    private static bool IsLoopback(string host) => string.Equals(host.TrimEnd('.'), "localhost", StringComparison.OrdinalIgnoreCase) || (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));
    private static string NormalizeApiKey(string? value)
    {
        var key = value?.Trim() ?? string.Empty;
        if (key.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) key = key[7..].Trim();
        return !string.IsNullOrWhiteSpace(key) ? key : throw new ArgumentException("apiKey is required.", nameof(value));
    }
    private static string RequestId(string? value) => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString() : value.Trim();
}
