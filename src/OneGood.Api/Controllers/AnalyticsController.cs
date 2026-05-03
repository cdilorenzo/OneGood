using Microsoft.AspNetCore.Mvc;
using OneGood.Core.Models;
using OneGood.Infrastructure.Data;

namespace OneGood.Api.Controllers
{
    public record TrackEventRequest(
        string EventType,
        string? UserAgent,
        string? BrowserLanguage,
        string? ActionDetail
    );

    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly OneGoodDbContext _db;
        public AnalyticsController(OneGoodDbContext db)
        {
            _db = db;
        }

        [HttpPost]
        public async Task<IActionResult> Track([FromBody] TrackEventRequest req)
        {
            var evt = new AnalyticsEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = req.EventType ?? string.Empty,
                UserAgent = req.UserAgent,
                BrowserLanguage = req.BrowserLanguage,
                ActionDetail = req.ActionDetail,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            };
            _db.AnalyticsEvents.Add(evt);
            await _db.SaveChangesAsync();
            return Ok();
        }
    }
}
