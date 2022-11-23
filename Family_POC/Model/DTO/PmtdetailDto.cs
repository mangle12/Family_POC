namespace Family_POC.Model.DTO
{
    public class PmtdetailDto
    {
        public decimal Saleprice { get; set; }

        public string Plu {get;set;}

        public List<PmtDto> Pmt { get; set; }
    }


    public class PmtDto
    { 
        public string Pmtno { get; set; }

        public string Pmtname { get; set; }

        public decimal Discount { get; set; }

        public decimal Disrate { get; set; }

        public decimal Qty { get; set; }
    }
}
