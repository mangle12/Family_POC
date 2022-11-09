namespace Family_POC.Interface
{
    public interface IPromotionService
    {
        public Task<List<FmActivity>> GetAllActivityAsync();

        public Task GetPromotionToRedisAsync();

        public Task GetPromotionPriceAsync(List<GetPromotionPriceReq> req);
    }
}
