using System.Text;
using Newtonsoft.Json;

namespace SalesApp.Services
{
    public class ScrapeJobRequest
    {
        [JsonProperty("store")]
        public string Store { get; set; } = string.Empty;

        [JsonProperty("matricula")]
        public string Matricula { get; set; } = string.Empty;

        [JsonProperty("callbackUrl")]
        public string CallbackUrl { get; set; } = string.Empty;

        [JsonProperty("jobId")]
        public string JobId { get; set; } = string.Empty;

        [JsonProperty("userId")]
        public string? UserId { get; set; }

        [JsonProperty("runId")]
        public string? RunId { get; set; }

        [JsonProperty("avaproUsername")]
        public string? AvaproUsername { get; set; }

        [JsonProperty("avaproPassword")]
        public string? AvaproPassword { get; set; }

        [JsonProperty("scrapeDate")]
        public string? ScrapeDate { get; set; }

        [JsonProperty("scrapeType")]
        public string? ScrapeType { get; set; }

        [JsonProperty("outputMode")]
        public string? OutputMode { get; set; }
    }

    public class PbiScraperClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _callbackBaseUrl;
        private readonly string? _workerSecret;

        public PbiScraperClient(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(300);
            _callbackBaseUrl = configuration["PbiScraper:CallbackBaseUrl"] ?? "http://salesapp-api:5000";
            _workerSecret = configuration["PbiScraper:WorkerSecret"]
                         ?? configuration["WORKER_SECRET"]
                         ?? Environment.GetEnvironmentVariable("WORKER_SECRET");
        }

        public async Task<string> EnqueueJobAsync(string jobId, string userId, string store, string matricula, string? avaproUsername = null, string? avaproPassword = null, string? runId = null, string? scrapeDate = null, string? scrapeType = null, string? outputMode = null)
        {
            var request = new ScrapeJobRequest
            {
                JobId = jobId,
                RunId = runId,
                UserId = userId,
                Store = store,
                Matricula = matricula,
                CallbackUrl = $"{_callbackBaseUrl}/api/scrape/callback",
                AvaproUsername = avaproUsername,
                AvaproPassword = avaproPassword,
                ScrapeDate = scrapeDate,
                ScrapeType = scrapeType,
                OutputMode = outputMode
            };

            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/jobs", content);

            response.EnsureSuccessStatusCode();

            var resultJson = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(resultJson);
            return result?.jobId?.ToString() ?? jobId;
        }

        public virtual async Task<(bool success, bool loginSuccess, string message, List<string>? steps, string? detectedStore, string? detectedStartDate)> TestAuthAsync(string matricula, string password, string? store = null, bool useCache = false)
        {
            var request = new
            {
                matricula = matricula,
                password = password,
                store = store,
                useCache = useCache
            };

            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
                var response = await _httpClient.PostAsync("/test-auth", content, cts.Token);

                var resultJson = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<dynamic>(resultJson);

                bool isSuccess = result?.success != null ? (bool)result.success : response.IsSuccessStatusCode;
                string? authStatus = result?.authStatus?.ToString();
                bool isWrongPassword = string.Equals(authStatus, "wrong-password", StringComparison.OrdinalIgnoreCase);
                bool loginSuccess = isSuccess || (!isWrongPassword && (result?.loginSuccess != null ? (bool)result.loginSuccess : false));
                string msg = result?.message?.ToString() ?? (isSuccess ? "Autenticação bem-sucedida" : "Falha na autenticação");
                string? detectedStore = result?.detectedStore?.ToString();
                string? detectedStartDate = result?.detectedStartDate?.ToString();
                
                List<string>? steps = null;
                if (result?.steps != null)
                {
                    try { steps = JsonConvert.DeserializeObject<List<string>>(result.steps.ToString()); }
                    catch { }
                }

                return (isSuccess, loginSuccess, msg, steps, detectedStore, detectedStartDate);
            }
            catch (OperationCanceledException)
            {
                return (false, true, "Tempo limite excedido ao testar credenciais no AVA PRO (25s). Tente novamente.", new List<string> { "[Timeout] O serviço de autenticação não respondeu dentro de 25 segundos." }, null, null);
            }
        }

        public virtual async Task<bool> CancelJobAsync(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId)) return false;

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var request = new HttpRequestMessage(HttpMethod.Post, $"/jobs/{Uri.EscapeDataString(jobId)}/cancel");
                if (!string.IsNullOrWhiteSpace(_workerSecret))
                {
                    request.Headers.Add("X-Worker-Secret", _workerSecret.Trim());
                }

                var response = await _httpClient.SendAsync(request, cts.Token);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
