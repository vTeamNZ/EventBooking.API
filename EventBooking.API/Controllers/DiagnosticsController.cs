using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace EventBooking.API.Controllers
{
    [Route("api/diagnostics")]
    [ApiController]
    public class DiagnosticsController : ControllerBase
    {
        [HttpGet("controllers")]
        public ActionResult<object> GetControllers()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var controllerTypes = assembly.GetTypes()
                    .Where(t => t.IsSubclassOf(typeof(ControllerBase)))
                    .Select(t => new { 
                        Name = t.Name, 
                        Namespace = t.Namespace,
                        FullName = t.FullName,
                        Routes = t.GetCustomAttributes<RouteAttribute>()?.Select(r => r.Template)?.ToArray()
                    })
                    .ToList();

                return Ok(new { 
                    AssemblyName = assembly.FullName,
                    ControllerCount = controllerTypes.Count,
                    Controllers = controllerTypes
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Error = ex.Message, Stack = ex.StackTrace });
            }
        }
    }
}
