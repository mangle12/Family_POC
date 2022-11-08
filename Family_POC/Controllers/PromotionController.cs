using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

namespace Family_POC.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PromotionController : ControllerBase
    {
        private readonly IDistributedCache _cache;

        public PromotionController(IDistributedCache cache)
        {
            _cache = cache;
        }



    }
}
