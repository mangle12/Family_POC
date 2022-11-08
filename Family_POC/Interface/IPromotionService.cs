namespace Family_POC.Interface
{
    public interface IPromotionService
    {
        public Task<List<FmActivity>> GetAllActivityAsync();

        public Task<GetPromotionRes> GetPromotionAsync(List<GetPromotionReq> req);
    }
}
