using System.ComponentModel;

namespace Family_POC.Enum
{
    public enum PromotionType
    {
        [Description("商品特價")]
        Product = 1,

        [Description("類別特價")]
        Category = 2,

        [Description("區間特價")]
        Interval = 3,

        [Description("組合品搭贈")]
        Combination = 4,

        [Description("配對搭贈")]
        Matching = 5,

    }
}
