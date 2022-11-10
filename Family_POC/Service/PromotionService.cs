namespace Family_POC.Service
{
    public class PromotionService : IPromotionService
    {
        private readonly IDistributedCache _cache;
        private readonly IDbService _dbService;

        public PromotionService(IDistributedCache cache, IDbService dbService)
        {
            _cache = cache;
            _dbService = dbService;
        }

        public async Task<List<FmActivity>> GetAllActivityAsync()
        {
            var activityList = await _dbService.GetAllAsync<FmActivity>("SELECT * FROM fm_activity", new { });
            return activityList;
        }

        public async Task GetPromotionToRedisAsync()
        {
            var promotionMainDto = new PromotionMainDto();

            var pmtList = new List<string> { "49233006" , "49233007", "49233008" };

            foreach (var item in pmtList)
            {
                #region ptm123
                var pmtPluDetailList = await _dbService.GetAllAsync<PromotionFromPmtPluDetailDto>("SELECT p_type, p_no, pluno, no_vip_saleoff FROM fm_pmt_plu_detail where pluno = @pluno", new { pluno = item });
                promotionMainDto.Pmt123 = new List<PromotionDetailDto>() { };

                foreach (var pmtPluDetail in pmtPluDetailList)
                {
                    var promotionDetailDto123 = new PromotionDetailDto();
                    var comboList123 = new List<ComboDto>();

                    promotionDetailDto123.Type = pmtPluDetail.P_Type.StringToEnum<PromotionType>().GetEnumDescription();
                    promotionDetailDto123.P_No = pmtPluDetail.P_No;

                    var comboDto123 = new ComboDto()
                    {
                        Pluno = item,
                        Qty = 1,
                        Saleoff = pmtPluDetail.No_Vip_Saleoff
                    };

                    comboList123.Add(comboDto123);
                    promotionDetailDto123.Combo = comboList123;
                    promotionMainDto.Pmt123.Add(promotionDetailDto123);
                }

                
                #endregion


                #region ptm45
                promotionMainDto.Pmt45 = new List<PromotionDetailDto>() { };

                // 取的固定組合單身主鍵
                var mixPluDetailPKList = await _dbService.GetAllAsync<PluPkDto>(@"SELECT a_no, p_type, p_no, pluno FROM fm_mix_plu_detail where pluno = @pluno", new { pluno = item });

                foreach (var mixPluDetailPK in mixPluDetailPKList)
                {
                    var promotionDetailDto45 = new PromotionDetailDto();
                    var comboList45 = new List<ComboDto>();

                    // 利用主鍵搜尋組合商品主檔
                    var mixPlu = await _dbService.GetAsync<PromotionFromMixPluDto>(@"SELECT p_mode, no_vip_fix_amount, vip_fix_amount, no_vip_saleoff, vip_saleoff FROM fm_mix_plu where a_no = @Ano and p_type = @Ptype and p_no = @Pno ",
                        new { Ano = mixPluDetailPK.A_No, Ptype = mixPluDetailPK.P_Type, Pno = mixPluDetailPK.P_No });

                    // 利用主鍵搜尋組合商品明細檔
                    var mixPluDetailList = await _dbService.GetAllAsync<PromotionFromPmtPluDetailDto>(@"SELECT a_no, p_type, p_no, pluno, qty FROM fm_mix_plu_detail where a_no = @Ano and p_type = @Ptype and p_no = @Pno ",
                        new { Ano = mixPluDetailPK.A_No, Ptype = mixPluDetailPK.P_Type, Pno = mixPluDetailPK.P_No });

                    foreach (var mixPluDetail in mixPluDetailList)
                    {                        
                        promotionDetailDto45.Type = mixPluDetail.P_Type.StringToEnum<PromotionType>().GetEnumDescription();
                        promotionDetailDto45.P_No = mixPluDetail.P_No;

                        var comboDto45 = new ComboDto()
                        {
                            Pluno = mixPluDetail.Pluno,
                            Qty = mixPluDetail.Qty
                        };
                        comboList45.Add(comboDto45);

                        promotionDetailDto45.Combo = comboList45;
                        promotionDetailDto45.SalePrice = mixPlu.P_Mode == "1" ? mixPlu.No_Vip_Fix_Amount : mixPlu.No_Vip_Saleoff; //P_Mode=1時取得No_Vip_Fix_Amount欄位/P_Mode=2時取得No_Vip_Saleoff欄位
                    }

                    promotionMainDto.Pmt45.Add(promotionDetailDto45);
                }
                                              
                #endregion


                // 促銷表新增至Redis
                await _cache.SetStringAsync(item, JsonSerializer.Serialize(promotionMainDto));
            }
        }

        public async Task GetPromotionPriceAsync(List<GetPromotionPriceReq> req)
        {

            // 取得組合促銷資料 form Redis
            await GetPmt45DetailOnRedis(req);
        }

        private async Task GetPmt45DetailOnRedis(List<GetPromotionPriceReq> req)
        {
            var promotionMainDto = new PromotionMainDto()
            { 
                Pmt123 = new List<PromotionDetailDto>(),
                Pmt45 = new List<PromotionDetailDto>(),
            };

            var inputPmtList = req.Select(x => x.Pluno).ToList();

            foreach (var item in req)
            {
                var promotionString = await _cache.GetStringAsync(item.Pluno);
                var redisDto = JsonSerializer.Deserialize<PromotionMainDto>(promotionString);

                foreach (var promotionDto in redisDto.Pmt45)
                {
                    var noContainRow = promotionDto.Combo.Where(x => !inputPmtList.Contains(x.Pluno)); // 搜尋出不再此次input的商品編號

                    // 若有不包含在此次input的商品編號,則不加入到陣列內
                    if (noContainRow.Count() == 0)
                        promotionMainDto.Pmt45.Add(promotionDto);
                }                
            }
            promotionMainDto.Pmt45 = promotionMainDto.Pmt45.Distinct(x => x.P_No).ToList(); // 過濾重複促銷方案

            var a = JsonSerializer.Serialize(promotionMainDto);

            var permuteLists = PermutationsUtil.Permute(promotionMainDto.Pmt45.Select(x => x.P_No).ToList());

            // 計算排列組合後商品數量
            var countLists = await GetPermuteCount(permuteLists, req, promotionMainDto);

            var priceList = await GetPermutePrice(permuteLists, countLists, promotionMainDto);

            // 原價
            var totalPrice = await GetTotalPrice(req);

            PrintResult(permuteLists, countLists, priceList, totalPrice); // 印出排列組合&組合數量


            int b =0;
        }

        /// <summary>
        /// 印出促銷排列組合、促銷組數
        /// </summary>
        /// <param name="permuteLists">促銷排列組合</param>
        /// <param name="countLists">促銷組數</param>
        private static void PrintResult(IList<IList<string>> permuteLists, IList<IList<int>> countLists, IList<decimal> priceList,  decimal totalPrice)
        {

            Console.WriteLine("pluno = 49233006, qty = 5, price = 100");
            Console.WriteLine("pluno = 49233007, qty = 3, price = 150");
            Console.WriteLine("pluno = 49233008, qty = 2, price = 200");

            Console.WriteLine("");

            Console.WriteLine("A0001 = 150");
            Console.WriteLine("A0002 = 200");
            Console.WriteLine("A0003 = 50");

            Console.WriteLine("");

            Console.WriteLine("[");
            for (int i = 0; i < permuteLists.Count; i++)
            {
                Console.WriteLine($"    [{string.Join(',', permuteLists[i])}] ({string.Join(',', countLists[i])}) (原價:{totalPrice} 促銷價:{Decimal.ToInt32(priceList[i])} 折扣:{totalPrice - Decimal.ToInt32(priceList[i])})");
            }

            Console.WriteLine("]");
        }

        /// <summary>
        /// 計算排列組合後組合數量
        /// </summary>
        /// <returns></returns>
        private async Task<IList<IList<int>>> GetPermuteCount(IList<IList<string>> permuteLists, List<GetPromotionPriceReq> req, PromotionMainDto promotionMainDto)
        {
            IList<IList<int>> countLists = new List<IList<int>>();

            // 取得每一組排列組合
            foreach (var permuteList in permuteLists)
            {
                var permuteCountList = new List<int>();
                
                var copyReq = req.ConvertAll(s => new GetPromotionPriceReq { Pluno = s.Pluno, Qty = s.Qty, Price = s.Price }).ToList();

                // 取得促銷方案
                foreach (var permute in permuteList)
                {
                    var pmt45ComboList = promotionMainDto.Pmt45.Where(x => x.P_No == permute).First().Combo;

                    // 扣除商品數量&計算組數
                    /// 取得輸入商品中符合促銷的商品組合
                    var currentPromotionPriceList = copyReq.Where(x => pmt45ComboList.Select(y => y.Pluno).Contains(x.Pluno)).ToList();
                    var promotionCount = 0;

                    while (currentPromotionPriceList.Select(x => x.Qty).All(y => y > 0)) // 如果輸入商品組合有一個不為0就多新增一組商品組合並扣除數量
                    {
                        foreach (var promotionPrice in currentPromotionPriceList)
                        {
                            var promotion = copyReq.Where(x => x.Pluno == promotionPrice.Pluno).First();
                            promotion.Qty -= pmt45ComboList.Where(x => x.Pluno == promotionPrice.Pluno).First().Qty;                        
                        }

                        // 促銷組數+1 
                        promotionCount++;                   
                    }

                    permuteCountList.Add(promotionCount);
                }

                countLists.Add(permuteCountList);
            }

            return countLists;
        }

        /// <summary>
        /// 計算促銷組合價錢
        /// </summary>
        /// <returns></returns>
        private async Task<IList<decimal>> GetPermutePrice(IList<IList<string>> permuteLists, IList<IList<int>> countLists, PromotionMainDto promotionMainDto)
        {
            IList<decimal> priceLists = new List<decimal>();
            
            // 取得每一組排列組合
            for (int i = 0; i < permuteLists.Count; i++)
            {
                decimal salePrice = 0;

                // 取得促銷
                for (int j = 0; j < permuteLists[i].Count; j++)
                {
                    var permutePrice = promotionMainDto.Pmt45.Where(x => x.P_No == permuteLists[i][j]).First().SalePrice; //取得促銷方案價錢
                    salePrice += permutePrice * countLists[i][j];
                }

                priceLists.Add(salePrice);
            }

            return priceLists;
        }

        /// <summary>
        /// 取得此次購買原價
        /// </summary>
        /// <returns></returns>
        private async Task<decimal> GetTotalPrice(List<GetPromotionPriceReq> req)
        {
            decimal totalPrice = 0;

            foreach (var promotionPriceReq in req)
            {
                totalPrice += promotionPriceReq.Qty * promotionPriceReq.Price;
            }

            return totalPrice;
        }
    }
}
