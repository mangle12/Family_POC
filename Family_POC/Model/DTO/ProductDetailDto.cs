namespace Family_POC.Model.DTO
{
    public class ProductDetailDto
    {
        public string Pluno { get; set; }

        public decimal Qty { get; set; }

        public decimal Price { get; set; } // 品號售價

        public decimal SalePrice { get; set; } //促銷計算後價錢
    }
}
