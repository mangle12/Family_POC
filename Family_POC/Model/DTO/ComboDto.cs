namespace Family_POC.Model.DTO
{
    public class ComboDto
    {
        public string Pluno { get; set; }

        public decimal Qty { get; set; }

        public decimal? Saleoff { get; set; } // 單品搭配折扣

        public string Plu_Type { get; set; } // 配對搭贈使用
    }
}
