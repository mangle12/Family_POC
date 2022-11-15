namespace Family_POC.Model.DTO
{
    public class MixPluMultipleDto
    {
        public string A_No { get; set; }

        public string P_Type { get; set; }

        public string P_No { get; set; }

        public int Seq { get; set; }

        public string P_Mode { get; set; }

        public decimal Mod_Qty { get; set; }

        public decimal No_Vip_Amount { get; set; }

        public decimal Vip_Amount { get; set; }

        public decimal No_Vip_Saleoff { get; set; }

        public decimal Vip_Saleoff { get; set; }

        public decimal No_Vip_Saleprice { get; set; }

        public decimal Vip_Saleprice { get; set; }

        public List<string> PlunoList { get; set; }
    }
}
