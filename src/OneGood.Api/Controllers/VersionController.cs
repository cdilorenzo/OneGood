using Microsoft.AspNetCore.Mvc;

namespace OneGood.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VersionController : ControllerBase
    {
        private readonly IConfiguration _config;
        public VersionController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var version = _config["AppVersion"] ?? "1.0.0";
            return Ok(new { version });
        }
    }
}
