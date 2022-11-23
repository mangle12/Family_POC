namespace Family_POC.Model.Response
{
    public class GetPromotionPriceResp
    {
        public decimal Totleprice { get; set; }

        public List<PmtdetailDto> Pmtdetail { get; set; }
    }
}
