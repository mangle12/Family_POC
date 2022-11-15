using System.Diagnostics;

namespace Family_POC.Service
{
    public class PromotionService : IPromotionService
    {
        private readonly IDistributedCache _cache;
        private readonly IDbService _dbService;
        private static decimal _totalPrice;
        private static IList<IList<string>> _permuteLists = new List<IList<string>>(); // (組合品/單品)促銷排列組合
        private static List<MixPluMultipleDto> _mixPluMultipleDtoLists = new List<MixPluMultipleDto>(); // 變動分量組合

        public PromotionService(IDistributedCache cache, IDbService dbService)
        {
            _cache = cache;
            _dbService = dbService;
            _totalPrice = 0;
        }

        public async Task<List<FmActivity>> GetAllActivityAsync()
        {
            var activityList = await _dbService.GetAllAsync<FmActivity>("SELECT * FROM fm_activity", new { });
            return activityList;
        }

        public async Task GetPromotionToRedisAsync()
        {
            var promotionMainDto = new PromotionMainDto();

            // 取得品號列表
            var pmtList = await _dbService.GetAllAsync<string>(@"SELECT plu_no FROM fm_plu", new { });

            #region 促銷方案

            foreach (var item in pmtList)
            {
                #region ptm123
                var pmtPluDetailList = await _dbService.GetAllAsync<PromotionFromPmtPluDetailDto>("SELECT a_no, p_type, p_no, pluno, no_vip_saleoff FROM fm_pmt_plu_detail where pluno = @pluno", new { pluno = item });
                promotionMainDto.Pmt123 = new List<PromotionDetailDto>() { };

                foreach (var pmtPluDetail in pmtPluDetailList)
                {
                    var promotionDetailDto123 = new PromotionDetailDto();
                    var comboList123 = new List<ComboDto>();

                    promotionDetailDto123.P_Key = string.Format($"{pmtPluDetail.A_No}_{pmtPluDetail.P_Type}_{pmtPluDetail.P_No}");
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

                // 取得固定組合單身主鍵
                var mixPluDetailPKList = await _dbService.GetAllAsync<PluPkDto>(@"SELECT a_no, p_type, p_no, pluno FROM fm_mix_plu_detail where pluno = @pluno", new { pluno = item });

                foreach (var mixPluDetailPK in mixPluDetailPKList)
                {
                    var promotionDetailDto45 = new PromotionDetailDto();
                    var comboList45 = new List<ComboDto>();

                    // 利用主鍵搜尋組合商品主檔
                    var mixPlu = await _dbService.GetAsync<PromotionFromMixPluDto>(@"SELECT p_mode, mix_mode, no_vip_fix_amount, vip_fix_amount, no_vip_saleoff, vip_saleoff FROM fm_mix_plu where a_no = @Ano and p_type = @Ptype and p_no = @Pno ",
                        new { Ano = mixPluDetailPK.A_No, Ptype = mixPluDetailPK.P_Type, Pno = mixPluDetailPK.P_No });

                    // 利用主鍵搜尋組合商品明細檔
                    var mixPluDetailList = await _dbService.GetAllAsync<PromotionFromPmtPluDetailDto>(@"SELECT a_no, p_type, p_no, pluno, qty FROM fm_mix_plu_detail where a_no = @Ano and p_type = @Ptype and p_no = @Pno ",
                        new { Ano = mixPluDetailPK.A_No, Ptype = mixPluDetailPK.P_Type, Pno = mixPluDetailPK.P_No });

                    foreach (var mixPluDetail in mixPluDetailList)
                    {
                        promotionDetailDto45.P_Key = string.Format($"{mixPluDetail.A_No}_{mixPluDetail.P_Type}_{mixPluDetail.P_No}");
                        promotionDetailDto45.Type = mixPluDetail.P_Type.StringToEnum<PromotionType>().GetEnumDescription();
                        promotionDetailDto45.P_No = mixPluDetail.P_No;
                        promotionDetailDto45.P_Mode = mixPlu.P_Mode;
                        promotionDetailDto45.Mix_Mode = mixPlu.Mix_Mode;

                        var comboDto45 = new ComboDto()
                        {
                            Pluno = mixPluDetail.Pluno,
                            Qty = mixPluDetail.Qty
                        };
                        comboList45.Add(comboDto45);

                        promotionDetailDto45.Combo = comboList45;

                        if (mixPlu.Mix_Mode == "1") // 固定組合促銷
                        {
                            promotionDetailDto45.SalePrice = mixPlu.P_Mode == "1" ? mixPlu.No_Vip_Fix_Amount : mixPlu.No_Vip_Saleoff; //P_Mode=1時取得No_Vip_Fix_Amount欄位/P_Mode=2時取得No_Vip_Saleoff欄位
                        }
                        
                    }

                    promotionMainDto.Pmt45.Add(promotionDetailDto45);
                }
                                              
                #endregion


                // 促銷表新增至Redis
                await _cache.SetStringAsync(item, JsonSerializer.Serialize(promotionMainDto));
            }

            #endregion

            #region 分量折扣資料檔

            // 取得品號列表
            var mixPluMultipleList = await _dbService.GetAllAsync<MixPluMultipleDto>(@"SELECT m.a_no, m.p_type, m.p_no, m.seq, m.mod_qty, m.no_vip_amount, m.vip_amount, m.no_vip_saleoff, m.vip_saleoff, m.no_vip_saleprice, m.vip_saleprice, p.p_mode FROM fm_mix_plu_multiple m
                                                                                    inner join fm_mix_plu p on p.p_no = m.p_no", new { });
            foreach (var mixPlu in mixPluMultipleList)
            {
                var key = string.Format($"{mixPlu.A_No}_{mixPlu.P_Type}_{mixPlu.P_No}");

                var valueList = new List<MixPluMultipleDto>();

                var pmtPluDetailList = await _dbService.GetAllAsync<string>("SELECT pluno FROM fm_mix_plu_detail where a_no = @aNo and p_type = @pType and p_no = @pNo ", new { aNo = mixPlu.A_No, pType = mixPlu.P_Type, pNo = mixPlu.P_No });

                var plunoList = new List<string>();
                foreach (var detail in pmtPluDetailList)
                {
                    plunoList.Add(detail);
                }

                var matchList = mixPluMultipleList.Where(x => x.A_No == mixPlu.A_No & x.P_Type == mixPlu.P_Type & x.P_No == mixPlu.P_No).ToList();
                foreach (var match in matchList)
                {
                    match.PlunoList = plunoList;
                    valueList.Add(match);
                }

                await _cache.SetStringAsync(key, JsonSerializer.Serialize(valueList));
            }
            #endregion
        }

        public async Task GetPromotionPriceAsync(List<GetPromotionPriceReq> req)
        {
            var sw = new Stopwatch();
            sw.Start();

            // 取得此次購買原價
            _totalPrice = await GetTotalPrice(req);

            // 取得組合促銷資料 form Redis
            await GetPmtDetailOnRedis(req);

            sw.Stop();
            Console.WriteLine($"耗時 : {sw.ElapsedMilliseconds} 豪秒");
        }

        private async Task GetPmtDetailOnRedis(List<GetPromotionPriceReq> req)
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

                // 組合品促銷
                foreach (var promotionDto in redisDto.Pmt45)
                {
                    var noContainRow45 = promotionDto.Combo.Where(x => !inputPmtList.Contains(x.Pluno)); // 搜尋出不在此次input的商品編號

                    // 若有不包含在此次input的商品編號,則不加入到促銷陣列內
                    if (!noContainRow45.Any())
                    {
                        if (promotionDto.Mix_Mode == "1") // 固定組合
                        {
                            if (promotionDto.P_Mode == "2") // 折扣
                            {
                                decimal permutePrice = 0;

                                // 計算組合品搭贈價錢
                                foreach (var combo in promotionDto.Combo)
                                {
                                    var inputPrice = req.Where(x => x.Pluno == combo.Pluno).First().Price;
                                    permutePrice += promotionDto.SalePrice * inputPrice * combo.Qty;
                                }

                                promotionDto.SalePrice = permutePrice;
                            }

                            promotionMainDto.Pmt45.Add(promotionDto);
                        }
                        else if (promotionDto.Mix_Mode == "2") // 變動分量組合
                        {
                            var mixPluMultipleDto = JsonSerializer.Deserialize<List<MixPluMultipleDto>>(await _cache.GetStringAsync(promotionDto.P_Key));
                            _mixPluMultipleDtoLists.AddRange(mixPluMultipleDto);
                        }                        
                    }
                }

                // 單品促銷
                if (redisDto.Pmt123.Count > 0)
                {
                    // 如果同品號有兩種單品促銷以上，選擇折扣率最大的單品促銷
                    var firstCombo = redisDto.Pmt123.Select(x => x.Combo.First()).OrderBy(y => y.Saleoff).First(); // 取得最大則扣率的Combo ( **每個PromotionDto只會有一個Combo** )
                    var firstPromotionDto = redisDto.Pmt123.Where(x => x.Combo.First() == firstCombo).First(); // 依據最大則扣率的Combo取得該筆PromotionDto

                    var noContainRow123 = firstPromotionDto.Combo.Where(x => !inputPmtList.Contains(x.Pluno)); // 搜尋出不在此次input的商品編號
                    if (!noContainRow123.Any())
                        promotionMainDto.Pmt123.Add(firstPromotionDto);
                }                
            }

            promotionMainDto.Pmt45 = promotionMainDto.Pmt45.Distinct(x => x.P_No).ToList(); // 過濾重複組合促銷
            _mixPluMultipleDtoLists = _mixPluMultipleDtoLists.DistinctBy(x => new { x.A_No, x.P_Type, x.P_No, x.Seq }).ToList(); // 過濾重複變動分量組合促銷

            var pNoList = promotionMainDto.Pmt45.Select(x => x.P_No).Union(_mixPluMultipleDtoLists.Select(x => x.P_No)).ToList(); // 取得此次Pmt45促銷

            // 計算排列組合
            _permuteLists = PermutationsUtil.Permute(pNoList);

            // 計算排列組合後商品數量
            var countLists = await GetPermuteCount(req, promotionMainDto);

            // 計算促銷組合價錢
            var priceList = await GetPermutePrice(countLists, promotionMainDto, req);

            PrintResult(countLists, priceList); // 印出排列組合&組合數量
        }

        /// <summary>
        /// 印出促銷排列組合、促銷組數
        /// </summary>
        /// <param name="permuteLists">促銷排列組合</param>
        /// <param name="countLists">促銷組數</param>
        private static void PrintResult(IList<IList<int>> countLists, IList<decimal> priceList)
        {
            Console.WriteLine("");

            Console.WriteLine("pluno = 24010013, qty = 5, price = 55");
            Console.WriteLine("pluno = 24010014, qty = 3, price = 65");
            Console.WriteLine("pluno = 24010001, qty = 2, price = 45");
            Console.WriteLine("pluno = 11010001, qty = 2, price = 10");

            Console.WriteLine("");

            Console.WriteLine("[");
            for (int i = 0; i < _permuteLists.Count; i++)
            {
                Console.WriteLine($"    [{string.Join(',', _permuteLists[i])}] ({string.Join(',', countLists[i])}) (原價:{_totalPrice} 促銷價:{Decimal.ToInt32(priceList[i])} 折扣:{_totalPrice - Decimal.ToInt32(priceList[i])})");
            }

            Console.WriteLine("]");
        }

        /// <summary>
        /// 計算排列組合後組合數量
        /// </summary>
        /// <param name="permuteLists">組合品促銷 排列組合</param>
        /// <param name="req">購買的商品Model</param>
        /// <param name="promotionMainDto">Redis內符合商品的促銷內容</param>
        /// <returns></returns>
        private async Task<IList<IList<int>>> GetPermuteCount(List<GetPromotionPriceReq> req, PromotionMainDto promotionMainDto)
        {
            IList<IList<int>> countLists = new List<IList<int>>();

            // 取得每一組排列組合
            foreach (var permuteList in _permuteLists)
            {
                var permuteCountList = new List<int>();

                // 複製req當作計算扣除組合促銷後的剩餘商品數量
                var copyReq = req.ConvertAll(s => new GetPromotionPriceReq { Pluno = s.Pluno, Qty = s.Qty, Price = s.Price }).ToList();

                // 取得單一組合促銷方案(Pmt45)
                foreach (var permute in permuteList)
                {
                    var pmt45List = promotionMainDto.Pmt45.Where(x => x.P_No == permute);

                    if (pmt45List.Count() > 0) // 固定組合
                    {
                        var pmt45ComboList = pmt45List.First().Combo;

                        // 扣除商品數量&計算組數
                        // 取得input商品中符合組合促銷的商品組合
                        var currentPromotionPriceList = copyReq.Where(x => pmt45ComboList.Select(y => y.Pluno).Contains(x.Pluno)).ToList();
                        var promotion45Count = 0; // 組合促銷組數

                        while (currentPromotionPriceList.Select(x => x.Qty).All(y => y > 0)) // 如果輸入商品組合數量都不為0就多新增一組商品組合並扣除數量
                        {
                            foreach (var promotionPrice in currentPromotionPriceList)
                            {
                                var promotion = copyReq.Where(x => x.Pluno == promotionPrice.Pluno).First();
                                promotion.Qty -= pmt45ComboList.Where(x => x.Pluno == promotionPrice.Pluno).First().Qty;
                            }

                            // 促銷組數+1 
                            promotion45Count++;
                        }

                        permuteCountList.Add(promotion45Count);
                    }
                    else // 變動分量組合
                    {
                        var mixPluMultipleDtoList = _mixPluMultipleDtoLists.Where(x => x.P_No == permute).OrderByDescending(x => x.Seq).ToList();

                        var plunoList = mixPluMultipleDtoList.Select(x => x.PlunoList).First();

                        // 扣除商品數量&計算組數
                        // 取得input商品中符合組合促銷的商品組合
                        var currentPromotionPriceList = copyReq.Where(x => plunoList.Select(y => y).Contains(x.Pluno)).ToList();
                        var promotionMultiCount = 0; // 組合促銷組數
                        var minModQty = mixPluMultipleDtoList[mixPluMultipleDtoList.Count - 1].Mod_Qty; //最小組數

                        while (currentPromotionPriceList.Select(x => x.Qty).Sum(x => x) > minModQty) // 同商品組合
                        {
                            foreach (var promotionPrice in currentPromotionPriceList)
                            {
                                var promotion = copyReq.Where(x => x.Pluno == promotionPrice.Pluno).First();
                                decimal result = 0;

                                foreach (var mixPluMultipleDto in mixPluMultipleDtoList) // 同商品
                                {
                                    result = Math.Floor(promotionPrice.Qty / mixPluMultipleDto.Mod_Qty);
                                    promotion.Qty = promotionPrice.Qty % mixPluMultipleDto.Mod_Qty;                                    
                                }

                                // 增加促銷組數
                                promotionMultiCount = decimal.ToInt32(promotionMultiCount + result);
                            }                            
                        }

                        if (currentPromotionPriceList.Select(x => x.Qty).Sum(x => x) == minModQty) // 不同商品組合
                        {
                            foreach (var promotionPrice in currentPromotionPriceList)
                            {
                                var promotion = copyReq.Where(x => x.Pluno == promotionPrice.Pluno).First();
                                promotion.Qty = promotion.Qty - 1;
                            }

                            // 增加促銷組數 
                            promotionMultiCount++;
                        }

                        permuteCountList.Add(promotionMultiCount);
                    }                                        
                }

                // 若input商品還有剩，則計算單品促銷(Pmt123)
                if (copyReq.Select(x => x.Qty).Any(y => y > 0))
                {                    
                    foreach (var item in copyReq)
                    {
                        if (item.Qty == 0)
                            continue;

                        var promotion123Count = 0; // 單品促銷組數

                        var pmt123 = promotionMainDto.Pmt123.Where(x => x.Combo.First().Pluno == item.Pluno).FirstOrDefault();

                        if(pmt123 ==  null) // 沒有單品促銷就跳過
                            continue;

                        var pmt123Combo = pmt123.Combo.First();

                        while (item.Qty > 0)
                        {
                            item.Qty = item.Qty - pmt123Combo.Qty;
                            promotion123Count++;
                        }

                        permuteCountList.Add(promotion123Count);

                        permuteList.Add(pmt123.P_No);
                    }                    
                }

                countLists.Add(permuteCountList);
            }

            return countLists;
        }

        /// <summary>
        /// 計算促銷組合價錢
        /// </summary>
        /// <returns></returns>
        private async Task<IList<decimal>> GetPermutePrice(IList<IList<int>> countLists, PromotionMainDto promotionMainDto, List<GetPromotionPriceReq> req)
        {
            IList<decimal> priceLists = new List<decimal>();
            
            // 取得每一組排列組合
            for (int i = 0; i < _permuteLists.Count; i++)
            {
                decimal salePrice = 0;

                for (int j = 0; j < _permuteLists[i].Count; j++)
                {
                    var pmt45 = promotionMainDto.Pmt45.Where(x => x.P_No == _permuteLists[i][j]).FirstOrDefault();
                    decimal permutePrice = 0;

                    if (pmt45 != null) // 取得組合品促銷方案(固定組合)價錢
                    {
                        permutePrice = pmt45.SalePrice;
                    }
                    else 
                    {                        
                        var mixPluMultipleDto = _mixPluMultipleDtoLists.Where(x => x.P_No == _permuteLists[i][j]).FirstOrDefault();

                        if (mixPluMultipleDto != null) // 取得組合品促銷方案(變動分量組合)價錢
                        {
                            if (mixPluMultipleDto.P_Mode == "1") // 特價
                            {
                                permutePrice = mixPluMultipleDto.No_Vip_Saleprice;
                            }
                            else if (mixPluMultipleDto.P_Mode == "2") // 折扣
                            {

                            }
                            else if (mixPluMultipleDto.P_Mode == "3") // 折價
                            { 
                            
                            }
                        }
                        else // 取得單品促銷方案價錢
                        {
                            var pmt123Combo = promotionMainDto.Pmt123.Where(x => x.P_No == _permuteLists[i][j]).First().Combo.First();
                            var pmt123Price = req.Where(x => x.Pluno == pmt123Combo.Pluno).First().Price;
                            permutePrice = (decimal)(pmt123Combo.Saleoff * pmt123Price);
                        }                        
                    }
                    
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
        private Task<decimal> GetTotalPrice(List<GetPromotionPriceReq> req)
        {
            decimal totalPrice = 0;

            foreach (var promotionPriceReq in req)
            {
                totalPrice += promotionPriceReq.Qty * promotionPriceReq.Price;
            }

            return Task.FromResult(totalPrice);
        }
    }
}
