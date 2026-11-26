using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lound.Desktop
{
    internal sealed class ApiClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include
        };

        public string BaseUrl { get; }
        public string AccessToken { get; private set; }

        public ApiClient(string baseUrl)
        {
            if (!Uri.TryCreate((baseUrl ?? string.Empty).Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException("Enter a valid HTTP or HTTPS API address.", nameof(baseUrl));
            if (uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback)
                throw new ArgumentException("Use HTTPS for a remote API. HTTP is allowed only for localhost or a loopback address.", nameof(baseUrl));

            BaseUrl = uri.ToString().TrimEnd('/');
            _http = new HttpClient { BaseAddress = new Uri(BaseUrl + "/"), Timeout = TimeSpan.FromSeconds(30) };
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<LoginResponse> LoginAsync(string email, string password)
        {
            var response = await SendAsync<LoginResponse>(HttpMethod.Post, "v1/auth/login", new { email, password }, false).ConfigureAwait(false);
            if (response == null || string.IsNullOrWhiteSpace(response.AccessToken))
                throw new ApiException("The sign-in response did not include an access token.", HttpStatusCode.BadGateway, "invalid_response");
            SetAccessToken(response.AccessToken);
            return response;
        }

        public async Task<UserDto> RegisterAsync(string email, string password, string displayName)
        {
            var response = await SendAsync<JObject>(HttpMethod.Post, "v1/auth/register", new { email, password, displayName }, false).ConfigureAwait(false);
            return response?["user"]?.ToObject<UserDto>() ?? response?.ToObject<UserDto>();
        }

        public void SetAccessToken(string token)
        {
            AccessToken = token;
            _http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
                ? null
                : new AuthenticationHeaderValue("Bearer", token);
        }

        public Task<T> GetAsync<T>(string path) => SendAsync<T>(HttpMethod.Get, path, null, true);
        public Task<T> PostAsync<T>(string path, object body = null) => SendAsync<T>(HttpMethod.Post, path, body, true);
        public Task<T> PatchAsync<T>(string path, object body) => SendAsync<T>(new HttpMethod("PATCH"), path, body, true);

        public async Task PostNoContentAsync(string path, object body = null)
        {
            await SendAsync<object>(HttpMethod.Post, path, body, true).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<T>> GetListAsync<T>(string path)
        {
            using (var response = await _http.GetAsync(path).ConfigureAwait(false))
            {
                var token = await ReadTokenAsync(response).ConfigureAwait(false);
                if (token == null || token.Type == JTokenType.Null)
                    return new List<T>();

                var listToken = token as JArray;
                if (listToken == null && token is JObject obj)
                {
                    listToken = obj["items"] as JArray ?? obj["results"] as JArray ?? obj["data"] as JArray;
                }

                if (listToken == null)
                    throw new ApiException("The server returned an unexpected list response.", HttpStatusCode.BadGateway, "invalid_response");

                return listToken.ToObject<List<T>>(JsonSerializer.Create(_jsonSettings)) ?? new List<T>();
            }
        }

        public async Task LogoutAsync()
        {
            try
            {
                await SendAsync<object>(HttpMethod.Post, "v1/auth/logout", null, true).ConfigureAwait(false);
            }
            finally
            {
                SetAccessToken(null);
            }
        }

        private async Task<T> SendAsync<T>(HttpMethod method, string path, object body, bool requireAuthentication)
        {
            using (var request = new HttpRequestMessage(method, path))
            {
                if (requireAuthentication && string.IsNullOrWhiteSpace(AccessToken))
                    throw new ApiException("Your session has ended. Sign in again.", HttpStatusCode.Unauthorized, "not_authenticated");

                if (body != null)
                    request.Content = new StringContent(JsonConvert.SerializeObject(body, _jsonSettings), Encoding.UTF8, "application/json");

                using (var response = await _http.SendAsync(request).ConfigureAwait(false))
                {
                    var token = await ReadTokenAsync(response).ConfigureAwait(false);
                    if (token == null || token.Type == JTokenType.Null)
                        return default(T);
                    return token.ToObject<T>(JsonSerializer.Create(_jsonSettings));
                }
            }
        }

        private async Task<JToken> ReadTokenAsync(HttpResponseMessage response)
        {
            var text = response.Content == null ? string.Empty : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            JToken token = null;
            if (!string.IsNullOrWhiteSpace(text))
            {
                try { token = JToken.Parse(text); }
                catch (JsonReaderException)
                {
                    if (!response.IsSuccessStatusCode)
                        throw new ApiException("The server returned an unreadable error response.", response.StatusCode, "invalid_error_response");
                    throw new ApiException("The server returned data the client could not read.", HttpStatusCode.BadGateway, "invalid_response");
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = token?.ToObject<ApiErrorDto>(JsonSerializer.Create(_jsonSettings));
                var message = error?.Message;
                if (string.IsNullOrWhiteSpace(message))
                    message = response.StatusCode == HttpStatusCode.Unauthorized
                        ? "Your session has ended. Sign in again."
                        : response.StatusCode == HttpStatusCode.Forbidden
                            ? "You do not have permission to do that."
                            : response.StatusCode == HttpStatusCode.Conflict
                                ? "This record changed or the requested action is no longer available."
                                : "The request could not be completed.";

                if (error?.Errors != null)
                {
                    var details = new List<string>();
                    foreach (var pair in error.Errors)
                        if (pair.Value != null)
                            details.AddRange(pair.Value);
                    if (details.Count > 0)
                        message += " " + string.Join(" ", details);
                }
                throw new ApiException(message, response.StatusCode, error?.Code);
            }

            return token;
        }

        public void Dispose() => _http.Dispose();
    }

    internal sealed class ApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string Code { get; }

        public ApiException(string message, HttpStatusCode statusCode, string code) : base(message)
        {
            StatusCode = statusCode;
            Code = code;
        }
    }
}
