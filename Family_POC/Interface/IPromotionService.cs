namespace Family_POC.Interface
{
    public interface IPromotionService
    {
        public Task GetPromotionToRedisAsync();

        public Task<GetPromotionPriceResp> GetPromotionPriceAsync(List<GetPromotionPriceReq> req);
    }
}
