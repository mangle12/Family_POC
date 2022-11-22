using Family_POC.Model.DTO;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using System.Diagnostics;
using System.Linq;

namespace Family_POC.Service
{
    public class PromotionService : IPromotionService
    {
        private readonly IDistributedCache _cache;
        private readonly IDbService _dbService;
        private static decimal _totalPrice; // 此次購買商品原總價
        private static IList<IList<string>> _permuteLists; // 符合促銷排列組合
        private static List<MixPluMultipleDto> _mixPluMultipleDtoLists; // 促銷變動分量組合
        private static Dictionary<string, List<MultipleCountDto>> _multipleCountDict; // 促銷變動分量組合組數
        private static Dictionary<string, List<GetPromotionPriceReq>> _productListsDict; // 促銷品項組合
        private static Dictionary<string, List<GetPromotionPriceReq>> _remainProductListsDict; // 剩餘品項組合

        public PromotionService(IDistributedCache cache, IDbService dbService)
        {
            _cache = cache;
            _dbService = dbService;
            _totalPrice = 0;

            _permuteLists = new List<IList<string>>();
            _mixPluMultipleDtoLists = new List<MixPluMultipleDto>();
            _multipleCountDict = new Dictionary<string, List<MultipleCountDto>>();
            _productListsDict = new Dictionary<string, List<GetPromotionPriceReq>>();
            _remainProductListsDict = new Dictionary<string, List<GetPromotionPriceReq>>();
        }

        public async Task GetPromotionToRedisAsync()
        {
            var promotionMainDto = new PromotionMainDto();

            // 取得品號列表
            var pmtList = await _dbService.GetAllAsync<PluDto>(@"SELECT plu_no,retailprice FROM fm_plu", new { });

            #region 促銷方案

            foreach (var item in pmtList)
            {
                #region ptm123
                var pmtPluDetailList = await _dbService.GetAllAsync<PromotionFromPmtPluDetailDto>("SELECT a_no, p_type, p_no, pluno, no_vip_saleoff FROM fm_pmt_plu_detail where pluno = @pluno", new { pluno = item.Plu_No });
                promotionMainDto.Pmt123 = new List<PromotionDetailDto>() { };

                foreach (var pmtPluDetail in pmtPluDetailList)
                {
                    var promotionDetailDto123 = new PromotionDetailDto();
                    var comboList123 = new List<ComboDto>();

                    promotionDetailDto123.P_Key = string.Format($"{pmtPluDetail.A_No}_{pmtPluDetail.P_Type}_{pmtPluDetail.P_No}");
                    promotionDetailDto123.P_Type = pmtPluDetail.P_Type;
                    promotionDetailDto123.P_No = pmtPluDetail.P_No;

                    var comboDto123 = new ComboDto()
                    {
                        Pluno = item.Plu_No,
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

                // 取得組合品搭贈單身主鍵
                var mixPluDetailPKList = await _dbService.GetAllAsync<PluPkDto>(@"SELECT a_no, p_type, p_no, pluno FROM fm_mix_plu_detail where pluno = @pluno", new { pluno = item.Plu_No });

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
                        promotionDetailDto45.P_Type = mixPluDetail.P_Type;
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
                            promotionDetailDto45.SalePrice = mixPlu.P_Mode == "1" ? mixPlu.No_Vip_Fix_Amount : mixPlu.No_Vip_Saleoff; // P_Mode=1時取得No_Vip_Fix_Amount欄位 / P_Mode=2時取得No_Vip_Saleoff欄位
                        }
                    }

                    promotionMainDto.Pmt45.Add(promotionDetailDto45);
                }

                //配對搭贈單身主鍵
                var mixAbpluDetailPKList = await _dbService.GetAllAsync<PluPkDto>(@"SELECT a_no, p_type, p_no, pluno, plu_type FROM fm_mix_abplu_detail where pluno = @pluno", new { pluno = item.Plu_No });

                foreach (var mixAbpluDetailPK in mixAbpluDetailPKList)
                {
                    var promotionDetailDto45 = new PromotionDetailDto();
                    var comboList45 = new List<ComboDto>();

                    // 利用主鍵搜尋組合商品主檔
                    var mixPlu = await _dbService.GetAsync<PromotionFromMixPluDto>(@"SELECT p_mode, mix_mode, no_vip_fix_amount, vip_fix_amount, no_vip_saleoff, vip_saleoff FROM fm_mix_plu where a_no = @Ano and p_type = @Ptype and p_no = @Pno ",
                        new { Ano = mixAbpluDetailPK.A_No, Ptype = mixAbpluDetailPK.P_Type, Pno = mixAbpluDetailPK.P_No });

                    // 利用主鍵搜尋配對促銷明細檔
                    var promotionFromMixAbpluDetailDtoList = await _dbService.GetAllAsync<PromotionFromMixAbpluDetailDto>(@"SELECT a_no, p_type, p_no, pluno, plu_type, qty FROM fm_mix_abplu_detail where a_no = @Ano and p_type = @Ptype and p_no = @Pno ",
                        new { Ano = mixAbpluDetailPK.A_No, Ptype = mixAbpluDetailPK.P_Type, Pno = mixAbpluDetailPK.P_No });

                    foreach (var mixAbpluDetail in promotionFromMixAbpluDetailDtoList)
                    {
                        promotionDetailDto45.P_Key = string.Format($"{mixAbpluDetail.A_No}_{mixAbpluDetail.P_Type}_{mixAbpluDetail.P_No}");
                        promotionDetailDto45.P_Type = mixAbpluDetail.P_Type;
                        promotionDetailDto45.P_No = mixAbpluDetail.P_No;
                        promotionDetailDto45.P_Mode = mixPlu.P_Mode;
                        promotionDetailDto45.Mix_Mode = mixPlu.Mix_Mode;

                        var comboDto45 = new ComboDto()
                        {
                            Pluno = mixAbpluDetail.Pluno,
                            Qty = mixAbpluDetail.Qty,
                            Plu_Type = mixAbpluDetail.Plu_Type
                        };
                        comboList45.Add(comboDto45);

                        promotionDetailDto45.Combo = comboList45;

                        if (mixPlu.Mix_Mode == "1") // 固定組合促銷
                        {
                            promotionDetailDto45.SalePrice = mixPlu.P_Mode == "1" ? mixPlu.No_Vip_Fix_Amount : mixPlu.No_Vip_Saleoff; // P_Mode=1時取得No_Vip_Fix_Amount欄位 / P_Mode=2時取得No_Vip_Saleoff欄位
                        }
                    }

                    promotionMainDto.Pmt45.Add(promotionDetailDto45);
                }
                #endregion


                // 促銷表新增至Redis
                await _cache.SetStringAsync(item.Plu_No, JsonSerializer.Serialize(promotionMainDto));
            }

            #endregion

            #region 分量折扣資料檔(p_type = 4)

            // 取得品號列表
            var mixPluMultipleList = await _dbService.GetAllAsync<MixPluMultipleDto>(@"SELECT m.a_no, m.p_type, m.p_no, m.seq, m.mod_qty, m.no_vip_amount, m.vip_amount, m.no_vip_saleoff, m.vip_saleoff, m.no_vip_saleprice, m.vip_saleprice, p.p_mode FROM fm_mix_plu_multiple m
                                                                                    inner join fm_mix_plu p on p.p_no = m.p_no", new { });
            foreach (var mixPlu in mixPluMultipleList)
            {
                var key = string.Format($"{mixPlu.A_No}_{mixPlu.P_Type}_{mixPlu.P_No}");

                var valueList = new List<MixPluMultipleDto>();

                var pmtPluDetailList = await _dbService.GetAllAsync<MixPluDetailDto>("SELECT pluno, qty FROM fm_mix_plu_detail where a_no = @aNo and p_type = @pType and p_no = @pNo ", new { aNo = mixPlu.A_No, pType = mixPlu.P_Type, pNo = mixPlu.P_No });

                var plunoList = new List<string>();
                var plunoQty = new List<decimal>();

                foreach (var detail in pmtPluDetailList)
                {
                    plunoList.Add(detail.Pluno); // 特價代號列表
                    plunoQty.Add(detail.Qty); // 數量倍數列表
                }

                var matchList = mixPluMultipleList.Where(x => x.A_No == mixPlu.A_No & x.P_Type == mixPlu.P_Type & x.P_No == mixPlu.P_No).ToList();
                foreach (var match in matchList)
                {
                    match.PlunoList = plunoList;
                    match.PlunoQty = plunoQty;

                    valueList.Add(match);
                }

                await _cache.SetStringAsync(key, JsonSerializer.Serialize(valueList));
            }
            #endregion

            #region 配對搭贈資料檔(p_type = 5)

            // 取得配對搭贈列表
            var mixAbpluDetailList = await _dbService.GetAllAsync<FmMixAbpluDetail>(@"SELECT m.a_no, m.p_type, m.p_no, m.plu_type, m.pluno, m.qty, p.p_mode FROM fm_mix_abplu_detail m
                                                                                    inner join fm_mix_plu p on p.p_no = m.p_no where p.p_type = '5'", new { });

            var distinctList = mixAbpluDetailList.DistinctBy(x => x.P_No).ToList();
            var mixAbpluDetailDtoList = new List<FmMixAbpluDetailDto>();

            foreach (var item in distinctList)
            {
                var fmMixAbpluDetailDto = new FmMixAbpluDetailDto();

                var pNoAbpluDetailList = mixAbpluDetailList.Where(x => x.P_No == item.P_No);

                var key = string.Format($"{item.A_No}_{item.P_Type}_{item.P_No}");

                var abpluDetailDtoList = new List<AbpluDetailDto>();

                foreach (var detail in pNoAbpluDetailList)
                { 
                    var abpluDetailDto = new AbpluDetailDto();
                    abpluDetailDto.Pluno = detail.Pluno;
                    abpluDetailDto.Qty = detail.Qty;
                    abpluDetailDto.Remark = detail.Remark;
                    abpluDetailDto.Ratio = detail.Ratio;

                    abpluDetailDtoList.Add(abpluDetailDto);
                }

                fmMixAbpluDetailDto.A_No = item.A_No;
                fmMixAbpluDetailDto.P_Type = item.P_Type;
                fmMixAbpluDetailDto.P_No = item.P_No;
                fmMixAbpluDetailDto.P_Mode = item.P_Mode;
                fmMixAbpluDetailDto.Plu_Type= item.Plu_Type;
                fmMixAbpluDetailDto.Detail = abpluDetailDtoList;

                await _cache.SetStringAsync(key, JsonSerializer.Serialize(fmMixAbpluDetailDto));
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
                var promotionString = await _cache.GetStringAsync(item.Pluno); // 取得Redis內此品號的促銷表

                if (promotionString == null)
                {
                    Console.WriteLine($"--無此商品: {item.Pluno} --");
                    return;
                }

                var redisDto = JsonSerializer.Deserialize<PromotionMainDto>(promotionString);
                
                foreach (var promotionDto in redisDto.Pmt45)
                {
                    if (promotionDto.P_Type == PromotionType.Combination.Value()) // 組合品搭贈
                    {
                        if (promotionDto.Mix_Mode == "1") // 固定組合
                        {
                            var noContainRow45 = promotionDto.Combo.Where(x => !inputPmtList.Contains(x.Pluno)); // 搜尋出不在此次input的商品編號

                            // 若有不包含在此次input的商品編號,則不加入到促銷陣列內
                            if (!noContainRow45.Any())
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
                        }
                        else if (promotionDto.Mix_Mode == "2") // 變動分量組合
                        {
                            var mixPluMultipleDto = JsonSerializer.Deserialize<List<MixPluMultipleDto>>(await _cache.GetStringAsync(promotionDto.P_Key));
                            _mixPluMultipleDtoLists.AddRange(mixPluMultipleDto);
                        }
                    }
                    else if (promotionDto.P_Type == PromotionType.Matching.Value()) // 配對搭贈
                    {
                        if (promotionDto.Mix_Mode == "1") // 固定組合
                        {
                            var noContainRow45 = promotionDto.Combo.Where(x => inputPmtList.Contains(x.Pluno)); // 搜尋包含input的商品編號

                            // 需同時符合A區和B區至少各一商品
                            if (noContainRow45.Where(x => x.Plu_Type == "1").Count() > 0 && noContainRow45.Where(x => x.Plu_Type == "2").Count() > 0)
                            {
                                promotionMainDto.Pmt45.Add(promotionDto);
                            }
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

            // 取得符合條件的促銷代號
            var pNoList = promotionMainDto.Pmt123.Select(x => x.P_No).Union(promotionMainDto.Pmt45.Select(x => x.P_No).Union(_mixPluMultipleDtoLists.Select(x => x.P_No))).ToList(); 

            // 計算促銷的排列組合
            _permuteLists = PermutationsUtil.Permute(pNoList);

            // 計算排列組合後商品數量
            var countLists = await GetPermuteCount(req, promotionMainDto);

            // 計算促銷組合價錢
            var priceList = await GetPermutePrice(countLists, promotionMainDto, req);

            // 印出排列組合 & 組合數量 & 價錢
            PrintResult(countLists, priceList, req); 
        }

        /// <summary>
        /// 印出促銷排列組合、促銷組數
        /// </summary>
        /// <param name="permuteLists">促銷排列組合</param>
        /// <param name="countLists">促銷組數</param>
        /// <param name="req">Request Input</param>
        private static void PrintResult(IList<IList<int>> countLists, IList<decimal> priceList, List<GetPromotionPriceReq> req)
        {
            Console.WriteLine("");

            foreach (var item in req)
            {
                Console.WriteLine(JsonSerializer.Serialize(item));
            }

            Console.WriteLine("");
            Console.WriteLine("---促銷排列組合---");

            Console.WriteLine("[");
            for (int i = 0; i < _permuteLists.Count; i++)
            {
                var math = Math.Round(Decimal.ToInt32(priceList[i]) / _totalPrice, 2); // 折扣率(四捨五入到小數點第二位)
                Console.WriteLine($"    [{string.Join(',', _permuteLists[i])}] ({string.Join(',', countLists[i])}) (原價:{_totalPrice} 折:{_totalPrice - Decimal.ToInt32(priceList[i])} 折扣率:{math} 實際銷售金額:{Decimal.ToInt32(priceList[i])} )");
            }            

            Console.WriteLine("]");

            Console.WriteLine("---促銷品項明細---");
            Console.WriteLine("[");
            for (int i = 0; i < _permuteLists.Count; i++)
            {
                var consoleString = "    {";

                for (int j = 0; j < _permuteLists[i].Count; j++)
                {
                    if (_productListsDict.ContainsKey($"{i}_{j}"))
                    {
                        var productList = _productListsDict[$"{i}_{j}"];

                        foreach (var product in productList)
                        {
                            if (product.Qty > 0)
                                consoleString += $"{product.Pluno} * {decimal.ToInt16(product.Qty)}, ";
                        }

                        if (productList.Count == 0)
                            consoleString += "null, ";

                        consoleString = consoleString.Remove(consoleString.Length - 2, 2);
                        if (j < _permuteLists[i].Count)
                            consoleString += " | ";
                    }                        
                }

                consoleString += "}";
                Console.WriteLine(consoleString);
            }
            Console.WriteLine("]");

            Console.WriteLine("---無法促銷品項明細---");
            Console.WriteLine("[");
            if (_permuteLists.Count > 0)
            {
                for (int i = 0; i < _permuteLists.Count; i++)
                {
                    var consoleString = "    {";

                    if (_remainProductListsDict.ContainsKey($"{i}"))
                    {
                        var remainProductList = _remainProductListsDict[$"{i}"];

                        foreach (var remainProduct in remainProductList)
                        {
                            consoleString += $"{remainProduct.Pluno} * {decimal.ToInt16(remainProduct.Qty)}, ";
                        }

                        consoleString = consoleString.Remove(consoleString.Length - 2, 2);
                    }

                    consoleString += "}";
                    Console.WriteLine(consoleString);
                }
            }
            else // 輸入商品都不符合促銷
            {
                var consoleString = "    {";
                var remainProductList = _remainProductListsDict[$"{0}"];

                foreach (var remainProduct in remainProductList)
                {
                    consoleString += $"{remainProduct.Pluno} * {decimal.ToInt16(remainProduct.Qty)}, ";
                }

                consoleString = consoleString.Remove(consoleString.Length - 2, 2);

                consoleString += $"}} (總價:{_totalPrice})";
                Console.WriteLine(consoleString);
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
            for (int i = 0; i < _permuteLists.Count; i++)
            {
                var permuteCountList = new List<int>();

                // 複製req當作計算扣除組合促銷後的剩餘商品數量
                var copyReq = await GetInputReq(req);

                // 取得單一組合促銷方案(Pmt45)
                for (int j = 0; j < _permuteLists[i].Count; j++)
                {
                    var subProductList = new List<GetPromotionPriceReq>();
                    var pmt45List = promotionMainDto.Pmt45.Where(x => x.P_No == _permuteLists[i][j]);

                    if (pmt45List.Any()) // 固定組合數量>0
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
                                var qty = pmt45ComboList.Where(x => x.Pluno == promotionPrice.Pluno).First().Qty;
                                promotion.Qty -= qty;

                                subProductList.Add(new GetPromotionPriceReq 
                                { 
                                    Pluno = promotion.Pluno,
                                    Qty = qty,
                                    Price = promotion.Price,
                                });
                            }

                            // 促銷組數+1 
                            promotion45Count++;
                        }

                        permuteCountList.Add(promotion45Count);
                    }
                    else // 變動分量組合
                    {
                        var mixPluMultipleDtoList = _mixPluMultipleDtoLists.Where(x => x.P_No == _permuteLists[i][j]).OrderByDescending(x => x.Seq).ToList();

                        if (mixPluMultipleDtoList.Count == 0) // 不符合組合促銷跳過
                            break;

                        var plunoList = mixPluMultipleDtoList.Select(x => x.PlunoList).First();

                        // 扣除商品數量&計算組數
                        // 取得input商品中符合組合促銷的商品組合
                        var currentPromotionPriceList = copyReq.Where(x => plunoList.Select(y => y).Contains(x.Pluno)).ToList();
                        var promotionMultiCount = 0; // 組合促銷組數
                        var minModQty = mixPluMultipleDtoList[mixPluMultipleDtoList.Count - 1].Mod_Qty; //最小組數

                        var dKey = mixPluMultipleDtoList[0].A_No + mixPluMultipleDtoList[0].P_Type + mixPluMultipleDtoList[0].P_No + i.ToString() + j.ToString();
                        var multipleCountDtoList = new List<MultipleCountDto>();

                        var promotionList = new List<string>(); // 依照傳入順序排列相同促銷的所有品項

                        foreach (var currentPromotion in currentPromotionPriceList)
                        {
                            for (int c = 0; c < currentPromotion.Qty; c++)
                            {
                                promotionList.Add(currentPromotion.Pluno);
                            }
                        }

                        decimal sumCount = promotionList.Count();
                        int index = 0;
                        
                        while (sumCount >= minModQty)
                        {
                            foreach (var mixPluMultipleDto in mixPluMultipleDtoList)
                            {
                                if (sumCount < mixPluMultipleDto.Mod_Qty) // 剩餘數量小於最小組數時跳出迴圈
                                {
                                    multipleCountDtoList.Add(new MultipleCountDto()
                                    {
                                        PSeq = mixPluMultipleDto.Seq,
                                        PCount = 0
                                    });                                   

                                    continue;
                                }
                                else
                                {
                                    multipleCountDtoList.Add(new MultipleCountDto()
                                    {
                                        PSeq = mixPluMultipleDto.Seq,
                                        PCount = 1
                                    });

                                    var result = Math.Floor(sumCount / mixPluMultipleDto.Mod_Qty);
                                    sumCount = sumCount % mixPluMultipleDto.Mod_Qty;

                                    for (int c = 0; c < result * mixPluMultipleDto.Mod_Qty; c++)
                                    {
                                        subProductList.Add(new GetPromotionPriceReq
                                        {
                                            Pluno = promotionList[index],
                                            Qty = 1,
                                            Price = copyReq.Where(x => x.Pluno == promotionList[index]).First().Price,
                                        });

                                        var promotion = copyReq.Where(x => x.Pluno == promotionList[index]).First();
                                        promotion.Qty = promotion.Qty - 1;

                                        index++;
                                    }

                                    // 增加促銷組數 
                                    promotionMultiCount += decimal.ToInt32(result);
                                }                                                                                                    
                            }
                        }

                        _multipleCountDict.Add(dKey, multipleCountDtoList);
                        
                        permuteCountList.Add(promotionMultiCount);
                    }

                    _productListsDict.Add($"{i}_{j}", subProductList); // 記錄此促銷扣除的品號及數量
                }

                // 若input商品還有剩，則計算單品促銷(Pmt123)
                if (copyReq.Select(x => x.Qty).Any(y => y > 0))
                {
                    var remainProductList = new List<GetPromotionPriceReq>();

                    foreach (var item in copyReq)
                    {
                        if (item.Qty == 0) // 數量為0跳過
                            continue;
                        
                        var pmt123 = promotionMainDto.Pmt123.Where(x => x.Combo.First().Pluno == item.Pluno).FirstOrDefault();

                        if (pmt123 == null) // 沒有單品促銷
                        {
                            remainProductList.Add(item);
                            continue;
                        }

                        // 符合單品促銷
                        var promotion123Count = 0; // 單品促銷組數
                        var pmt123Combo = pmt123.Combo.First();

                        while (item.Qty > 0)
                        {
                            item.Qty = item.Qty - pmt123Combo.Qty;
                            promotion123Count++;
                        }

                        permuteCountList.Add(promotion123Count);
                    }

                    _remainProductListsDict.Add($"{i}", remainProductList);
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

            if (_permuteLists.Count > 0)
            {
                // 取得每一組排列組合
                for (int i = 0; i < _permuteLists.Count; i++)
                {
                    decimal salePrice = 0;
                    decimal discountPrice = 0;

                    // 複製req當作計算扣除組合促銷後的剩餘商品數量
                    var copyReq = await GetInputReq(req);

                    for (int j = 0; j < _permuteLists[i].Count; j++)
                    {
                        var pmt45 = promotionMainDto.Pmt45.Where(x => x.P_No == _permuteLists[i][j]).FirstOrDefault();
                        decimal permutePrice = 0;

                        if (_productListsDict.ContainsKey($"{i}_{j}"))
                        {
                            var permuteDetail = _productListsDict[$"{i}_{j}"]; // 促銷組合品項明細

                            if (pmt45 != null) // 取得組合品促銷方案(固定組合)價錢
                            {
                                permutePrice = pmt45.SalePrice * countLists[i][j];

                                // 計算扣除組合促銷後的剩餘商品數量                        
                                foreach (var permute in permuteDetail)
                                {
                                    var promotion = copyReq.Where(x => x.Pluno == permute.Pluno).First();
                                    promotion.Qty -= permute.Qty;
                                }
                            }
                            else
                            {
                                var mixPluMultipleDtoList = _mixPluMultipleDtoLists.Where(x => x.P_No == _permuteLists[i][j]).ToList();

                                if (mixPluMultipleDtoList.Count > 0) // 取得組合品促銷方案(變動分量組合)價錢
                                {
                                    var firstMixPluMultipleDto = mixPluMultipleDtoList.First(); // 取得第一筆mixPluMultipleDto

                                    var dKey = firstMixPluMultipleDto.A_No + firstMixPluMultipleDto.P_Type + firstMixPluMultipleDto.P_No + i.ToString() + j.ToString();
                                    var multipleCountDtoList = _multipleCountDict[dKey]; //取得組合數量

                                    if (firstMixPluMultipleDto.P_Mode == "1" & multipleCountDtoList.Count > 0) // 特價
                                    {
                                        foreach (var mixPluMultipleDto in mixPluMultipleDtoList)
                                        {
                                            var pCount = multipleCountDtoList.Where(x => x.PSeq == firstMixPluMultipleDto.Seq).First().PCount; //組合數量
                                            permutePrice += firstMixPluMultipleDto.No_Vip_Saleprice;
                                        }

                                        permutePrice = permutePrice * countLists[i][j]; // (價錢 * 組數)
                                    }
                                    else if (firstMixPluMultipleDto.P_Mode == "2" & multipleCountDtoList.Count > 0) // 折扣
                                    {
                                        var reqPlunoList = copyReq.Where(x => firstMixPluMultipleDto.PlunoList.Contains(x.Pluno)).ToList();
                                        decimal totalPrice = 0; // 總金額
                                        decimal tempPrice = 0; // 折扣價格

                                        foreach (var mixPluMultipleDto in mixPluMultipleDtoList)
                                        {
                                            // 計算總金額
                                            foreach (var reqPluno in reqPlunoList)
                                            {
                                                var curCount = permuteDetail.Where(x => x.Pluno == reqPluno.Pluno).Sum(x => x.Qty); //剩餘數量

                                                totalPrice += curCount * reqPluno.Price;

                                                var promotion = copyReq.Where(x => x.Pluno == reqPluno.Pluno).First();
                                                promotion.Qty -= curCount;
                                            }

                                            tempPrice += totalPrice * mixPluMultipleDto.No_Vip_Saleoff; // 折扣價格 (總金額 * 折扣率)
                                        }

                                        permutePrice = tempPrice;
                                    }
                                    else if (firstMixPluMultipleDto.P_Mode == "3" & multipleCountDtoList.Count > 0) // 折價
                                    {
                                        var reqPlunoList = copyReq.Where(x => firstMixPluMultipleDto.PlunoList.Contains(x.Pluno)).ToList(); //剩餘商品數量
                                        decimal discount = 0; //變動分量組合折價金額

                                        var minModQty = mixPluMultipleDtoList[mixPluMultipleDtoList.Count - 1].Mod_Qty; //最小組數
                                        var sumCount = reqPlunoList.Sum(x => x.Qty);

                                        while (sumCount >= minModQty)
                                        {
                                            foreach (var mixPluMultipleDto in mixPluMultipleDtoList)
                                            {
                                                if (sumCount < mixPluMultipleDto.Mod_Qty) // 剩餘數量小於最小組數時跳出迴圈
                                                {
                                                    continue;
                                                }
                                                else
                                                {
                                                    var pCount = multipleCountDtoList.Where(x => x.PSeq == mixPluMultipleDto.Seq).First().PCount; //組合數量
                                                    discount = pCount * mixPluMultipleDto.No_Vip_Amount; // 折價金額 (組數 * 折扣金額)

                                                    var result = Math.Floor(sumCount / mixPluMultipleDto.Mod_Qty); // 購買商品符合的組數
                                                    sumCount = sumCount % mixPluMultipleDto.Mod_Qty; // 剩餘商品數量

                                                    discountPrice += discount * result; // 總折價價格 (折價價格 * 符合組數)
                                                }
                                            }
                                        }

                                        var totalCount = reqPlunoList.Sum(x => x.Qty); // 總數量

                                        // 計算金額
                                        foreach (var reqPluno in reqPlunoList)
                                        {                                            
                                            for (int z = 0; z < reqPluno.Qty; z++)
                                            {
                                                if (totalCount - sumCount == 0) // 總數量-剩餘數量等於0時跳出迴圈
                                                    break;

                                                permutePrice += reqPluno.Price; // 計算已計算的商品價錢
                                                totalCount--;
                                            }                                        
                                        }
                                    }
                                }
                                else
                                {
                                    var pmt123 = promotionMainDto.Pmt123.Where(x => x.P_No == _permuteLists[i][j]);

                                    if (pmt123.Any())// 取得單品促銷方案價錢
                                    {
                                        var pmt123Combo = pmt123.First().Combo.First();
                                        var reqPluno = copyReq.Where(x => x.Pluno == pmt123Combo.Pluno).First();
                                        permutePrice = (decimal)(pmt123Combo.Saleoff * reqPluno.Price) * countLists[i][j]; // (價錢 * 組數)

                                        var promotion = copyReq.Where(x => x.Pluno == reqPluno.Pluno).First();
                                        promotion.Qty -= reqPluno.Qty;
                                    }
                                }
                            }

                            salePrice += permutePrice;
                        }
                    }

                    // 取得剩餘無法促銷單品價錢
                    if (_remainProductListsDict.ContainsKey($"{i}"))
                    {
                        decimal permutePrice = 0;
                        var remainProductList = _remainProductListsDict[$"{i}"]; // 促銷組合品項明細

                        foreach (var remainProduct in remainProductList)
                        {
                            permutePrice = remainProduct.Qty * remainProduct.Price;// (剩餘品項數量 * 品項價格)
                            salePrice += permutePrice;
                        }                        
                    }

                    if (discountPrice > 0)
                        salePrice -= discountPrice;

                    priceLists.Add(salePrice);
                }
            }
            else // 輸入商品都沒有符合一筆促銷
            {
                var remainProductList = new List<GetPromotionPriceReq>();

                foreach (var item in req)
                {
                    if (item.Qty == 0) // 數量為0跳過
                        continue;

                    remainProductList.Add(item);
                }

                _remainProductListsDict.Add($"{0}", remainProductList);
            }

            

            return priceLists;
        }

        /// <summary>
        /// 取得此次購買原價
        /// </summary>
        /// <returns></returns>
        private static Task<decimal> GetTotalPrice(List<GetPromotionPriceReq> req)
        {
            decimal totalPrice = 0;

            foreach (var promotionPriceReq in req)
            {
                totalPrice += promotionPriceReq.Qty * promotionPriceReq.Price;
            }

            return Task.FromResult(totalPrice);
        }

        private async Task<List<GetPromotionPriceReq>> GetInputReq(List<GetPromotionPriceReq> req)
        {
            var copyReq = new List<GetPromotionPriceReq>();

            foreach (var promotionReq in req)
            {
                var promotionList = req.Where(x => x.Pluno == promotionReq.Pluno);

                if (!copyReq.Exists(x => x.Pluno == promotionReq.Pluno))
                {
                    copyReq.Add(new GetPromotionPriceReq
                    {
                        Pluno = promotionReq.Pluno,
                        Qty = promotionList.Sum(x => x.Qty),
                        Price = promotionReq.Price,
                    });
                }
            }

            return copyReq;
        }
    }
}
