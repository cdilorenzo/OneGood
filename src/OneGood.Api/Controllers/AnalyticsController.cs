using Microsoft.AspNetCore.Mvc;
using OneGood.Core.Models;
using OneGood.Infrastructure.Data;

namespace OneGood.Api.Controllers
{
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
        public async Task<IActionResult> Track([FromBody] AnalyticsEvent evt)
        {
            evt.Timestamp = DateTime.UtcNow;
            _db.AnalyticsEvents.Add(evt);
            await _db.SaveChangesAsync();
            return Ok();
        }
    }
}
