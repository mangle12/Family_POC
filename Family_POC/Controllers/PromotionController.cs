

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

        /// <summary>
        /// 將促銷表寫入Redis(背景執行)
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetPromotionToRedis")]
        public async Task<IActionResult> GetPromotionToRedis()
        {
            await _promotionService.GetPromotionToRedisAsync();

            return Ok();
        }

        /// <summary>
        /// 取得促銷最佳解
        /// </summary>
        /// <returns></returns>
        [HttpPost("GetPromotionPrice")]
        public async Task<IActionResult> GetPromotionPrice(List<GetPromotionPriceReq> req)
        {
            await _promotionService.GetPromotionPriceAsync(req);

            return Ok();
        }

    }
}
