namespace Family_POC.Model.Entity
{
    public class EntityBase
    {
        [Column("flag")]
        public string? Flag { get; set; }

        [Column("create_date")]
        public DateTime CreateDate { get; set; }

        [Column("create_user")]
        public string? CreateUser { get; set; }

        [Column("modi_date")]
        public DateTime ModiDate { get; set; }

        [Column("modi_user")]
        public string? ModiUser { get; set; }
    }
}
