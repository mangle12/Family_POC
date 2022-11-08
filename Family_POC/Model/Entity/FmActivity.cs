namespace Family_POC.Model.Entity
{
    public class FmActivity : EntityBase
    {
        [Key, Column("a_no")]
        public string ANo { get; set; }

        [Column("a_name")]
        public string? AName { get; set; }

        [Column("a_fullname")]
        public string? AFullname { get; set; }

        [Column("sdate")]
        public DateTime Sdate { get; set; }

        [Column("edate")]
        public DateTime Edate { get; set; }
    }
}
