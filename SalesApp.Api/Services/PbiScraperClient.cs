using System.Text;
using Newtonsoft.Json;

namespace SalesApp.Services
{
    public class ScrapeJobRequest
    {
        public string Store { get; set; } = string.Empty;
        public string Matricula { get; set; } = string.Empty;
        public string CallbackUrl { get; set; } = string.Empty;
        public string JobId { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? AvaproUsername { get; set; }
        public string? AvaproPassword { get; set; }
    }

    public class PbiScraperClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _callbackBaseUrl;

        public PbiScraperClient(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _callbackBaseUrl = configuration["PbiScraper:CallbackBaseUrl"] ?? "http://salesapp-api:5000";
        }

        public async Task<string> EnqueueJobAsync(string jobId, string userId, string store, string matricula, string? avaproUsername = null, string? avaproPassword = null)
        {
            var request = new ScrapeJobRequest
            {
                JobId = jobId,
                UserId = userId,
                Store = store,
                Matricula = matricula,
                CallbackUrl = $"{_callbackBaseUrl}/api/scrape/callback",
                AvaproUsername = avaproUsername,
                AvaproPassword = avaproPassword
            };

            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/jobs", content);

            response.EnsureSuccessStatusCode();

            var resultJson = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(resultJson);
            return result?.jobId?.ToString() ?? jobId;
        }

        public async Task<(bool success, string message)> TestAuthAsync(string matricula, string password)
        {
            var request = new
            {
                matricula = matricula,
                password = password
            };

            var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/test-auth", content);

            var resultJson = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(resultJson);

            return (response.IsSuccessStatusCode, result?.message?.ToString() ?? (response.IsSuccessStatusCode ? "Sucesso" : "Falha na autenticação"));
        }
    }
}
