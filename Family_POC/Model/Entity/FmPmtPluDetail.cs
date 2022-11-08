namespace Family_POC.Model.Entity
{
    public class FmPmtPluDetail : EntityBase
    {
        [Key, Column("a_no", Order = 0)]
        public string ANo { get; set; }

        [Key, Column("p_type", Order = 1)]
        public string PType { get; set; }

        [Key, Column("p_no", Order = 2)]
        public string PNo { get; set; }

        [Key, Column("seq", Order = 3)]
        public int Seq { get; set; }

        [Key, Column("pluno", Order = 4)]
        public string Pluno { get; set; }

        [Column("no_vip_disc")]
        public decimal NoVipDisc { get; set; }

        [Column("vip_disc")]
        public decimal VipDisc { get; set; }

        [Column("no_vip_saleoff")]
        public decimal NoVipSaleoff { get; set; }

        [Column("vip_saleoff")]
        public decimal VipSaleoff { get; set; }

        [Column("is_vip")]
        public string? IsVip { get; set; }

        [Column("remark")]
        public string? Remark { get; set; }
    }
}
