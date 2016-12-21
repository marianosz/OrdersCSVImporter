using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OrdersCSVImporter.API.Client
{
    public abstract class BaseJsonServiceClient
    {
        string apiKey;
        string baseServiceUrl;
        ILogger<BaseJsonServiceClient> logger;
        bool onlyLogRequests;

        public BaseJsonServiceClient(APIServiceConfig apiServiceConfig, ILogger<BaseJsonServiceClient> logger, bool onlyLogRequests = false)
        {
            this.apiKey = apiServiceConfig.APIKey;
            this.baseServiceUrl = apiServiceConfig.URL;
            this.logger = logger;
            this.onlyLogRequests = onlyLogRequests;
        }

        virtual public string ApiKeyHederName => "api_key";

        public abstract Task<string> GetErrorMessage(string jsonResponse);

        public async Task<APIRequestResult<R>> Post<T, R>(string url, T data)
        {
            try
            {
                var requestMessage = new HttpRequestMessage(new HttpMethod("POST"), baseServiceUrl + url);

                requestMessage.Content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");

                using (var client = GetHttpClient())
                {
                    var httpResponseMessage = await client.SendAsync(requestMessage);

                    var jsonResponse = await httpResponseMessage.Content.ReadAsStringAsync();

                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        return new APIRequestResult<R>
                        {
                            Data = await DeserializeObject<R>(jsonResponse),
                            StatusCode = httpResponseMessage.StatusCode
                        };
                    }

                    return new APIRequestResult<R>
                    {
                        Data = default(R),
                        ErrorMessage = await GetErrorMessageInternal(jsonResponse),
                        HasError = true,
                        StatusCode = httpResponseMessage.StatusCode
                    };
                }
            }
            catch (Exception ex)
            {
                return new APIRequestResult<R>
                {
                    Data = default(R),
                    ErrorMessage = ex.Message,
                    HasError = true,
                };
            }
        }
        public async Task<APIRequestResult> Post<T>(string url, T data)
        {
            try
            {
                var requestMessage = new HttpRequestMessage(new HttpMethod("POST"), baseServiceUrl + url);

                requestMessage.Content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");

                using (var client = GetHttpClient())
                {
                    var httpResponseMessage = await client.SendAsync(requestMessage);

                    var jsonResponse = await httpResponseMessage.Content.ReadAsStringAsync();

                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        return new APIRequestResult
                        {
                            StatusCode = httpResponseMessage.StatusCode
                        };
                    }

                    return new APIRequestResult
                    {
                        ErrorMessage = await GetErrorMessageInternal(jsonResponse),
                        HasError = true,
                        StatusCode = httpResponseMessage.StatusCode
                    };
                }
            }
            catch (Exception ex)
            {
                return new APIRequestResult
                {
                    ErrorMessage = ex.Message,
                    HasError = true,
                };
            }
        }

        public async Task<APIRequestResult<T>> Delete<T>(string url)
        {
            try
            {
                var requestMessage = new HttpRequestMessage(new HttpMethod("DELETE"), baseServiceUrl + url);

                using (var client = GetHttpClient())
                {
                    var httpResponseMessage = await client.SendAsync(requestMessage);

                    var jsonResponse = await httpResponseMessage.Content.ReadAsStringAsync();

                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        return new APIRequestResult<T>
                        {
                            Data = await DeserializeObject<T>(jsonResponse),
                            StatusCode = httpResponseMessage.StatusCode
                        };
                    }

                    return new APIRequestResult<T>
                    {
                        Data = default(T),
                        ErrorMessage = await GetErrorMessageInternal(jsonResponse),
                        HasError = true,
                        StatusCode = httpResponseMessage.StatusCode
                    };
                }
            }
            catch (Exception ex)
            {
                return new APIRequestResult<T>
                {
                    Data = default(T),
                    ErrorMessage = ex.Message,
                    HasError = true,
                };
            }
        }

        public async Task<APIRequestResult<T>> Get<T>(string url)
        {
            try
            {
                var requestMessage = new HttpRequestMessage(new HttpMethod("GET"), baseServiceUrl + url);

                using (var client = GetHttpClient())
                {
                    var httpResponseMessage = await client.SendAsync(requestMessage);

                    var jsonResponse = await httpResponseMessage.Content.ReadAsStringAsync();

                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        return new APIRequestResult<T>
                        {
                            Data = await DeserializeObject<T>(jsonResponse),
                            StatusCode = httpResponseMessage.StatusCode
                        };
                    }

                    return new APIRequestResult<T>
                    {
                        Data = default(T),
                        ErrorMessage = await GetErrorMessageInternal(jsonResponse),
                        HasError = true,
                        StatusCode = httpResponseMessage.StatusCode
                    };
                }
            }
            catch (Exception ex)
            {
                return new APIRequestResult<T>
                {
                    Data = default(T),
                    ErrorMessage = ex.Message,
                    HasError = true,
                };
            }
        }

        private HttpClient GetHttpClient()
        {
            HttpClient client;

            if (this.onlyLogRequests)
            {
                client = new HttpClient(new APICallLoggingHandler(new HttpClientHandler(), logger));
            }
            else
            {
                client = new HttpClient();
            }

            client.BaseAddress = new Uri(baseServiceUrl);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            client.DefaultRequestHeaders.Add(ApiKeyHederName, apiKey);

            return client;
        }

        public static async Task<T> DeserializeObject<T>(string json)
        {
            return await Task.Factory.StartNew(() => JsonConvert.DeserializeObject<T>(json));
        }

        private async Task<string> GetErrorMessageInternal(string jsonResponse)
        {
            var genericErrorMessage = "Error in the API service call";

            try
            {
                var errorMessage = await GetErrorMessage(jsonResponse);

                return $"{genericErrorMessage}: {errorMessage}";
            }
            catch
            {
                if (string.IsNullOrEmpty(jsonResponse))
                    return genericErrorMessage;

                return $"{genericErrorMessage}: {jsonResponse}";
            }
        }
    }


    class APICallLoggingHandler : DelegatingHandler
    {
        ILogger<BaseJsonServiceClient> logger;

        public APICallLoggingHandler(HttpMessageHandler innerHandler, ILogger<BaseJsonServiceClient> logger)
            : base(innerHandler)
        {
            this.logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await LogAPIRequest(request);

            //var response = await base.SendAsync(request, cancellationToken);

            //await LogAPIResponse(response);

            return new HttpResponseMessage() { StatusCode = System.Net.HttpStatusCode.OK, Content = new StringContent("{status:'OK'}")};
        }

        private async Task LogAPIRequest(HttpRequestMessage r)
        {
            var sb = new StringBuilder();

            sb.Append($"Request {r.Method} to {r.RequestUri}");

            if (r.Content != null)
            {
                sb.Append($" - Content {await r.Content.ReadAsStringAsync()}");
            }

            logger.LogTrace(sb.ToString());
        }

        private async Task LogAPIResponse(HttpResponseMessage r)
        {
            var sb = new StringBuilder();

            sb.Append($"Response of {r.RequestMessage.Method} from {r.RequestMessage.RequestUri} - Status Code {r.StatusCode}");

            if (r.Content != null)
            {
                sb.Append($" - Content {await r.Content.ReadAsStringAsync()}");
            }

            logger.LogTrace(sb.ToString());
        }
    }
}