

namespace Family_POC.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PromotionController : BaseApiController
    {        
        private readonly IPromotionService _promotionService;

        public PromotionController(IPromotionService promotionService)
        {
            _promotionService = promotionService;
        }

        [HttpGet]
        public async Task<ResponseResult<List<FmActivity>>> GetAllActivity()
        {
            var result = await _promotionService.GetAllActivityAsync();

            return SuccessResult(result);
        }

        [HttpGet("GetPromotionToRedis")]
        public async Task<IActionResult> GetPromotionToRedis()
        {
            await _promotionService.GetPromotionToRedisAsync();

            return Ok();
        }

        [HttpPost("GetPromotionPrice")]
        public async Task<IActionResult> GetPromotionPrice(List<GetPromotionPriceReq> req)
        {
            await _promotionService.GetPromotionPriceAsync(req);

            return Ok();
        }

    }
}
