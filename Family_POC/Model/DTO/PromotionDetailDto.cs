namespace Family_POC.Model.DTO
{
    public class PromotionDetailDto
    {
        public string P_Key { get; set; }

        public string Type { get; set; }

        public string P_No { get; set; }

        public string P_Mode { get; set; }

        public string Mix_Mode { get; set; }

        public List<ComboDto> Combo { get; set; }      
        
        public decimal SalePrice { get; set; }
    }
}
