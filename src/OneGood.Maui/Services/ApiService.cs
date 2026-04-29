using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace OneGood.Maui.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;
    public string BaseUrl { get; set; } = "https://localhost:7013"; // TODO: Set your API base URL

    public ApiService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<List<CauseSummaryDto>> GetCausesAsync()
    {
        var url = $"{BaseUrl}/api/actions/causes";
        return await _httpClient.GetFromJsonAsync<List<CauseSummaryDto>>(url) ?? new List<CauseSummaryDto>();
    }

    public async Task<CauseSummaryDto?> GetCauseAsync(string id)
    {
        var url = $"{BaseUrl}/api/actions/{id}";
        return await _httpClient.GetFromJsonAsync<CauseSummaryDto>(url);
    }
}

