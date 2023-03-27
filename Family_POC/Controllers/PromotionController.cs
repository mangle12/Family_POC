

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
        public async Task<ResponseResult<GetPromotionPriceResp>> GetPromotionPrice(List<GetPromotionPriceReq> req)
        {
            var result = await _promotionService.GetPromotionPriceAsync(req);

            return SuccessResult(result);
        }

        [HttpGet("health")]
        public IActionResult GetHealthCheck()
        { 
            return Ok("Service Success");
        }

    }
}
