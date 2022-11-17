namespace Family_POC.Interface
{
    public interface IPromotionService
    {
        public Task GetPromotionToRedisAsync();

        public Task GetPromotionPriceAsync(List<GetPromotionPriceReq> req);
    }
}
