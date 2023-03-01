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
        /// 229套餐
        /// </summary>
        /// <returns></returns>
        [Test]
        public async Task GetPromotionPriceTest()
        {

            var req = new List<GetPromotionPriceReq>()
            {
                new GetPromotionPriceReq()
                {
                    Pluno = "27010001",
                    Qty = 2,
                    Price = 69
                },
                new GetPromotionPriceReq()
                {
                    Pluno = "27010003",
                    Qty = 2,
                    Price = 50
                },
                new GetPromotionPriceReq()
                {
                    Pluno = "27010005",
                    Qty = 1,
                    Price = 25
                },
                new GetPromotionPriceReq()
                {
                    Pluno = "27010007",
                    Qty = 1,
                    Price = 27
                },
                new GetPromotionPriceReq()
                {
                    Pluno = "27010009",
                    Qty = 1,
                    Price = 25
                },
                new GetPromotionPriceReq()
                {
                    Pluno = "27010012",
                    Qty = 1,
                    Price = 140
                }
            };

            IDistributedCache cache = new Mock<IDistributedCache>().Object;
            IDbService dbService = new Mock<IDbService>().Object;
            PromotionService ps = new PromotionService(_cache!, dbService);

            var result = await ps.GetPromotionPriceAsync(req);

            Assert.AreEqual(299, decimal.ToInt32(result.Totalprice));
        }
    }
}
