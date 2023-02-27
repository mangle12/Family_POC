using Moq;
using NUnit.Framework;

namespace Family_POC.Test
{
    [TestFixture()]
    public class PromotionControllerTest
    {
        //private IPromotionService _promotionService;

        [SetUp]
        public void SetUp()
        {
            //var serviceProvider = new ServiceCollection()
            //.AddLogging()
            //.BuildServiceProvider();

            //_promotionService = serviceProvider.GetService<IPromotionService>()!;
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
            PromotionService ps = new PromotionService(cache, dbService);

            var result = await ps.GetPromotionPriceAsync(req);

            Assert.AreEqual(229, result.Totalprice);
        }
    }
}
