namespace Family_POC.Model.DTO
{
    public class FmMixAbpluDetailDto
    {
        public string A_No { get; set; }

        public string P_Type { get; set; }

        public string P_No { get; set; }

        public string P_Name { get; set; }

        public string P_Mode { get; set; }

        public string Plu_Type { get; set; }

        public List<AbpluDetailDto> Detail { get; set; }
    }

    /// <summary>
    /// // 配對搭贈組合
    /// </summary>
    public class AbpluDetailDto
    {
        public string Pluno { get; set; }

        public decimal Qty { get; set; }

        public string Remark { get; set; }

        public decimal Ratio { get; set; }
    }
}
