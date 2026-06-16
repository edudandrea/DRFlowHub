using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UniFlowHub.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/weather")]
    public class WeatherController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public WeatherController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("reverse")]
        public Task<IActionResult> Reverse([FromQuery] double latitude, [FromQuery] double longitude, CancellationToken cancellationToken)
        {
            var query = new Dictionary<string, string>
            {
                ["latitude"] = latitude.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["longitude"] = longitude.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["count"] = "1",
                ["language"] = "pt",
                ["format"] = "json",
            };

            return ProxyAsync("https://geocoding-api.open-meteo.com/v1/reverse", query, cancellationToken);
        }

        [HttpGet("forecast")]
        public Task<IActionResult> Forecast([FromQuery] double latitude, [FromQuery] double longitude, CancellationToken cancellationToken)
        {
            var query = new Dictionary<string, string>
            {
                ["latitude"] = latitude.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["longitude"] = longitude.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["daily"] = "weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max",
                ["timezone"] = "America/Sao_Paulo",
                ["forecast_days"] = "4",
            };

            return ProxyAsync("https://api.open-meteo.com/v1/forecast", query, cancellationToken);
        }

        private async Task<IActionResult> ProxyAsync(string baseUrl, Dictionary<string, string> query, CancellationToken cancellationToken)
        {
            if (!IsValidCoordinate(query["latitude"], -90, 90) || !IsValidCoordinate(query["longitude"], -180, 180))
                return BadRequest("Coordenadas invalidas.");

            using var http = _httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(8);

            var url = QueryString.Create(query.Select(item => new KeyValuePair<string, string?>(item.Key, item.Value))).ToUriComponent();
            using var response = await http.GetAsync($"{baseUrl}{url}", cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            return new ContentResult
            {
                Content = content,
                ContentType = "application/json",
                StatusCode = (int)response.StatusCode,
            };
        }

        private static bool IsValidCoordinate(string value, double min, double max)
        {
            return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                && parsed >= min
                && parsed <= max;
        }
    }
}
