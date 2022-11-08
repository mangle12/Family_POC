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

        [HttpPost("GetPromotion")]
        public async Task<ResponseResult<GetPromotionRes>> GetPromotion(List<GetPromotionReq> req)
        {
            var result = await _promotionService.GetPromotionAsync(req);

            return SuccessResult(result);
        }

    }
}
