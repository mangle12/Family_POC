namespace Family_POC.Model.Entity
{
    public class FmPmt : EntityBase
    {
        [Key, Column("a_no", Order = 0)]
        public string ANo { get; set; }

        [Key, Column("p_type", Order = 1)]
        public string PType { get; set; }

        [Key, Column("p_no", Order = 2)]
        public string PNo { get; set; }

        [Column("p_name")]
        public string? PName { get; set; }

        [Column("p_check_date")]
        public DateTime PCheckDate { get; set; }

        [Column("p_check_uid")]
        public string? PCheckUid { get; set; }

        [Column("p_mode")]
        public string? PMode { get; set; }

        [Column("sdate")]
        public DateOnly Sdate { get; set; }

        [Column("edate")]
        public DateOnly Edate { get; set; }

        [Column("stime")]
        public TimeOnly Stime { get; set; }

        [Column("etime")]
        public TimeOnly Etime { get; set; }

        [Column("acc_amounts")]
        public string? AccAmounts { get; set; }

        [Column("all_disc")]
        public string? AllDisc { get; set; }

        [Column("limit_plu")]
        public string? LimitPlu { get; set; }

        [Column("limit_type")]
        public string? LimitType { get; set; }

        [Column("limit_channel")]
        public string? LimitChannel { get; set; }

        [Column("is_fullpmt")]
        public string? IsFullpmt { get; set; }

        [Column("saleoff")]
        public decimal Saleoff { get; set; }

        [Column("vip_saleoff")]
        public decimal VipSaleoff { get; set; }

        [Column("vip_rank")]
        public string? VipRank { get; set; }

        [Column("limit_week")]
        public string? LimitWeek { get; set; }

    }
}
