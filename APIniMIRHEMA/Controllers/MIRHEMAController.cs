using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace APIniMIRHEMA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MIRHEMAController : ControllerBase
    {
        [HttpGet]
        public string SayHelloAPI()
        { 
            return "Hello from the API!";
        }
    }
}
