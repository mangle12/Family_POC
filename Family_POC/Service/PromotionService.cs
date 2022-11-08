

namespace Family_POC.Service
{
    public class PromotionService : IPromotionService
    {
        private readonly IDistributedCache _cache;
        private readonly IDbService _dbService;

        public PromotionService(IDistributedCache cache, IDbService dbService)
        {
            _cache = cache;
            _dbService = dbService;
        }

        public async Task<List<FmActivity>> GetAllActivityAsync()
        {
            var activityList = await _dbService.GetAllAsync<FmActivity>("SELECT * FROM fm_activity", new { });
            return activityList;
        }


        public async Task<GetPromotionRes> GetPromotionAsync(List<GetPromotionReq> req)
        {
            var getPromotionRes = new GetPromotionRes();
            var pmt123 = new List<ComboDto>();

            foreach (var item in req)
            {
                var pmtPluDetail = await _dbService.GetAsync<FmPmtPluDetail>("SELECT * FROM fm_pmt_plu_detail where pluno = @pluno", new { pluno = item.Pluno });

                var comboDto = new ComboDto()
                { 
                    Pluno = item.Pluno,
                    qty = 1,
                    saleoff = pmtPluDetail.NoVipSaleoff
                };

                pmt123.Add(comboDto);

                // 新增至Redis
                await _cache.SetStringAsync(item.Pluno, JsonSerializer.Serialize(comboDto));
            }

            getPromotionRes.Pmt123 = pmt123;

            return getPromotionRes;
        }
    }
}
