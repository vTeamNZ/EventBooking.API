using Microsoft.AspNetCore.Mvc;

namespace EventBooking.API.Controllers
{
    [Route("api/testorganizer")]
    [ApiController]
    public class TestOrganizerController : ControllerBase
    {
        [HttpGet("ping")]
        public ActionResult<string> Ping()
        {
            return Ok("TestOrganizerController is working!");
        }
    }
}
