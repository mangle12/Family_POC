using Moq;
using NUnit.Framework;

namespace Family_POC.Test
{
    [TestFixture()]
    public class PromotionControllerTest
    {
        private IDistributedCache? _cache;

        [SetUp]
        public void SetUp()
        {
            // 建立redis連線
            var services = new ServiceCollection();            
            services.AddStackExchangeRedisCache(o => { o.Configuration = "10.20.30.208:6379"; });
            var provider = services.BuildServiceProvider();
            _cache = provider.GetService<IDistributedCache>();
        }

        /// <summary>
        /// 套餐促銷
        /// 600套餐
        /// </summary>
        /// <returns></returns>
        [Test]
        public async Task GetPromotionPriceTest()
        {
            #region 促銷輸入
            var req = new List<GetPromotionPriceReq>()
            {
                new GetPromotionPriceReq()
                {
                    Pluno = "3820579",
                    Qty = 1,
                    Price = 69
                },
                new GetPromotionPriceReq()
                {
                    Pluno = "3820583",
                    Qty = 1,
                    Price = 69
                },
                new GetPromotionPriceReq()
                {
                    Pluno = "4010018",
                    Qty = 1,
                    Price = 50
                },
                new GetPromotionPriceReq()
                {
                    Pluno = "4010313",
                    Qty = 1,
                    Price = 50
                },
                new GetPromotionPriceReq()
                {
                    Pluno = "3831768",
                    Qty = 1,
                    Price = 25
                },
                new GetPromotionPriceReq()
                {
                    Pluno = "3832728",
                    Qty = 1,
                    Price = 25
                },
                new GetPromotionPriceReq()
                {
                    Pluno = "3830099",
                    Qty = 1,
                    Price = 25
                },
                new GetPromotionPriceReq()
                {
                    Pluno = "0089633",
                    Qty = 1,
                    Price = 35
                },
                new GetPromotionPriceReq()
                {
                    Pluno = "4310094",
                    Qty = 1,
                    Price = 35
                },
                new GetPromotionPriceReq()
                {
                    Pluno = "3320874",
                    Qty = 1,
                    Price = 140
                },
                new GetPromotionPriceReq()
                {
                    Pluno = "3221531",
                    Qty = 1,
                    Price = 116
                },
                new GetPromotionPriceReq()
                {
                    Pluno = "3120250",
                    Qty = 1,
                    Price = 100
                },
                new GetPromotionPriceReq()
                {
                    Pluno = "0135850",
                    Qty = 1,
                    Price = 72
                },
                new GetPromotionPriceReq()
                {
                    Pluno = "0135850",
                    Qty = 1,
                    Price = 72
                },
                new GetPromotionPriceReq()
                {
                    Pluno = "0135955",
                    Qty = 1,
                    Price = 55
                },
                new GetPromotionPriceReq()
                {
                    Pluno = "0135955",
                    Qty = 1,
                    Price = 55
                }
            };

            #endregion

            IDistributedCache cache = new Mock<IDistributedCache>().Object;
            IDbService dbService = new Mock<IDbService>().Object;
            PromotionService ps = new PromotionService(_cache!, dbService);

            var result = await ps.GetPromotionPriceAsync(req);

            Assert.AreEqual(600, decimal.ToInt32(result.Totalprice));
        }
    }
}
