using Family_POC.Model.DTO;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Numerics;
using System.Reflection;

namespace Family_POC.Service
{
    public class PromotionService : IPromotionService
    {
        private readonly IDistributedCache _cache;
        private readonly IDbService _dbService;
        private decimal _totalPrice; // 此次購買商品原總價
        private IList<IList<string>> _permuteLists; // 符合促銷排列組合
        private IList<decimal> _priceList; // 促銷計算後價格
        private IList<IList<int>> _countLists; // 促銷數量
        private List<MixPluMultipleDto> _mixPluMultipleDtoLists; // 促銷變動分量組合
        private readonly Dictionary<string, List<MultipleCountDto>> _multipleCountDict; // 促銷變動分量組合組數
        private readonly Dictionary<string, List<ProductDetailDto>> _productListsDict; // 促銷品項組合
        private readonly Dictionary<string, decimal> _permutePriceListsDict; // 促銷排列組合價錢
        private readonly Dictionary<string, List<GetPromotionPriceReq>> _remainProductListsDict; // 剩餘品項組合

        public PromotionService(IDistributedCache cache, IDbService dbService)
        {
            _cache = cache;
            _dbService = dbService;
            _totalPrice = 0;

            _permuteLists = new List<IList<string>>();
            _priceList = new List<decimal>();
            _countLists = new List<IList<int>>();
            _mixPluMultipleDtoLists = new List<MixPluMultipleDto>();
            _multipleCountDict = new Dictionary<string, List<MultipleCountDto>>();
            _productListsDict = new Dictionary<string, List<ProductDetailDto>>();
            _permutePriceListsDict = new Dictionary<string, decimal>();
            _remainProductListsDict = new Dictionary<string, List<GetPromotionPriceReq>>();
        }

        public async Task GetPromotionToRedisAsync()
        {
            var promotionMainDto = new PromotionMainDto();

            // 取得品號列表
            var pmtList = await _dbService.GetAllAsync<PluDto>(@"SELECT plu_no,retailprice FROM fm_plu", new { });

            #region 促銷方案
            var dataList = await _dbService.GetAllAsync<PromotionDataDto>(@"SELECT a_no, p_type, p_no, p_name, p_mode, 'Y' as is_same_plu, '0' as mix_mode FROM fm_pmt
                                                                    union all
                                                                    SELECT a_no, p_type, p_no, p_name, p_mode, is_same_plu, mix_mode FROM fm_mix_plu", new { });

            // 促銷列表新增至Redis
            await _cache.SetStringAsync("Promotion", JsonSerializer.Serialize(dataList));

            #endregion

            #region 各商品促銷方案

            foreach (var item in pmtList)
            {
                #region ptm123
                var pmtPluDetailList = await _dbService.GetAllAsync<PromotionFromPmtPluDetailDto>(@"SELECT d.a_no, d.p_type, d.p_no, p.p_name, d.pluno, d.no_vip_saleoff FROM fm_pmt_plu_detail d
                                                                                                    inner join fm_pmt p on p.p_no = d.p_no where d.pluno = @pluno", new { pluno = item.Plu_No });
                promotionMainDto.Pmt123 = new List<PromotionDetailDto>() { };

                foreach (var pmtPluDetail in pmtPluDetailList)
                {
                    var promotionDetailDto123 = new PromotionDetailDto();
                    var comboList123 = new List<ComboDto>();

                    promotionDetailDto123.P_Key = string.Format($"{pmtPluDetail.A_No}_{pmtPluDetail.P_Type}_{pmtPluDetail.P_No}");
                    promotionDetailDto123.P_Type = pmtPluDetail.P_Type;
                    promotionDetailDto123.P_No = pmtPluDetail.P_No;
                    promotionDetailDto123.P_Name = pmtPluDetail.P_Name;

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
                    var mixPlu = await _dbService.GetAsync<PromotionFromMixPluDto>(@"SELECT p_mode, mix_mode, is_same_plu, no_vip_fix_amount, vip_fix_amount, no_vip_saleoff, vip_saleoff FROM fm_mix_plu where a_no = @Ano and p_type = @Ptype and p_no = @Pno ",
                        new { Ano = mixPluDetailPK.A_No, Ptype = mixPluDetailPK.P_Type, Pno = mixPluDetailPK.P_No });

                    // 利用主鍵搜尋組合商品明細檔
                    var mixPluDetailList = await _dbService.GetAllAsync<PromotionFromPmtPluDetailDto>(@"SELECT d.a_no, d.p_type, d.p_no, p.p_name, d.pluno, d.qty, d.match, d.group FROM fm_mix_plu_detail d
                                                                                                        inner join fm_mix_plu p on p.p_no = d.p_no where d.a_no = @Ano and d.p_type = @Ptype and d.p_no = @Pno ",
                        new { Ano = mixPluDetailPK.A_No, Ptype = mixPluDetailPK.P_Type, Pno = mixPluDetailPK.P_No });

                    foreach (var mixPluDetail in mixPluDetailList)
                    {
                        promotionDetailDto45.P_Key = string.Format($"{mixPluDetail.A_No}_{mixPluDetail.P_Type}_{mixPluDetail.P_No}");
                        promotionDetailDto45.P_Type = mixPluDetail.P_Type;
                        promotionDetailDto45.P_No = mixPluDetail.P_No;
                        promotionDetailDto45.P_Name = mixPluDetail.P_Name;
                        promotionDetailDto45.P_Mode = mixPlu.P_Mode;
                        promotionDetailDto45.Mix_Mode = mixPlu.Mix_Mode;

                        if (mixPluDetail.Match != null && mixPluDetail.Match > 0) // 套餐促銷
                        {
                            var groupComboDto45 = comboList45.Where(x => x.Group == mixPluDetail.Group).FirstOrDefault();

                            if (groupComboDto45 != null)
                            {
                                groupComboDto45.Pluno = groupComboDto45.Pluno + "," + mixPluDetail.Pluno;
                            }
                            else
                            {
                                var comboDto45 = new ComboDto()
                                {
                                    Pluno = mixPluDetail.Pluno,
                                    Qty = mixPluDetail.Qty,
                                    Match = mixPluDetail.Match,
                                    Group = mixPluDetail.Group,
                                };

                                comboList45.Add(comboDto45);
                            }
                        }
                        else
                        {
                            var comboDto45 = new ComboDto()
                            {
                                Pluno = mixPluDetail.Pluno,
                                Qty = mixPluDetail.Qty,
                                Match = mixPluDetail.Match,
                                Group = mixPluDetail.Group,
                            };

                            comboList45.Add(comboDto45);
                        }

                        promotionDetailDto45.Combo = comboList45;

                        promotionDetailDto45.SalePrice = mixPlu.P_Mode == "1" ? mixPlu.No_Vip_Fix_Amount : mixPlu.No_Vip_Saleoff; // P_Mode=1時取得No_Vip_Fix_Amount欄位 / P_Mode=2時取得No_Vip_Saleoff欄位
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
                    var mixPlu = await _dbService.GetAsync<PromotionFromMixPluDto>(@"SELECT p_mode, mix_mode, is_same_plu, no_vip_fix_amount, vip_fix_amount, no_vip_saleoff, vip_saleoff FROM fm_mix_plu where a_no = @Ano and p_type = @Ptype and p_no = @Pno ",
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

                // 商品促銷表新增至Redis
                await _cache.SetStringAsync(item.Plu_No, JsonSerializer.Serialize(promotionMainDto));
            }

            #endregion

            #region 分量折扣資料檔(p_type = 4)

            // 取得品號列表
            var mixPluMultipleList = await _dbService.GetAllAsync<MixPluMultipleDto>(@"SELECT m.a_no, m.p_type, m.p_no, p.p_name, p.mix_mode, p.is_same_plu, m.seq, m.mod_qty, m.no_vip_amount, m.vip_amount, m.no_vip_saleoff, m.vip_saleoff, m.no_vip_saleprice, m.vip_saleprice, p.p_mode FROM fm_mix_plu_multiple m
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
            var mixAbpluDetailList = await _dbService.GetAllAsync<FmMixAbpluDetail>(@"SELECT m.a_no, m.p_type, m.p_no, p.p_name, m.plu_type, m.pluno, m.qty, p.p_mode, p.is_same_plu FROM fm_mix_abplu_detail m
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
                    abpluDetailDto.Plu_Type = detail.Plu_Type;

                    abpluDetailDtoList.Add(abpluDetailDto);
                }

                fmMixAbpluDetailDto.A_No = item.A_No;
                fmMixAbpluDetailDto.P_Type = item.P_Type;
                fmMixAbpluDetailDto.P_No = item.P_No;
                fmMixAbpluDetailDto.P_Name = item.P_Name;
                fmMixAbpluDetailDto.P_Mode = item.P_Mode;
                fmMixAbpluDetailDto.Detail = abpluDetailDtoList;

                await _cache.SetStringAsync(key, JsonSerializer.Serialize(fmMixAbpluDetailDto));
            }

            #endregion
        }

        public async Task<GetPromotionPriceResp> GetPromotionPriceAsync(List<GetPromotionPriceReq> req)
        {
            var totalSw = new Stopwatch();
            totalSw.Start();

            // 取得此次購買原價
            _totalPrice = await GetTotalPrice(req);            

            // 取得組合促銷資料 form Redis
            await GetPmtDetailOnRedis(req);

            var t2Sw = new Stopwatch();
            t2Sw.Start();
            // 取得促銷方案最優解
            var getPromotionPriceResp = await GetOptimalSolution(req);
            t2Sw.Stop();
            Console.WriteLine($"計算最優解耗時 : {t2Sw.ElapsedMilliseconds} 豪秒");

            totalSw.Stop();

            Console.WriteLine($"總耗時 : {totalSw.ElapsedMilliseconds} 豪秒");
            Console.WriteLine($"  ");

            return getPromotionPriceResp;
        }

        /// <summary>
        /// 取得促銷方案最優解
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        public async Task<GetPromotionPriceResp> GetOptimalSolution(List<GetPromotionPriceReq> req)
        {
            decimal resultPrice = 0;
            var pmtdetailList = new List<PmtdetailDto>();

            if (_permuteLists.Count > 0)
            {
                resultPrice = _priceList.Min();

                var index = _priceList.IndexOf(_priceList.Min()); // 選出最優解(實際銷售金額最低金額)
                var permuteList = _permuteLists[index];
                var permuteDetailString = string.Empty;
                var tempPmtDtoList = new List<TempPmtDto>();
                var nowIndex = 0; // 第幾個品號

                // 取得符合促銷的全部品項
                var permuteProductList = await GetAllPermuteProductList(permuteList, index);

                foreach (var reqPluno in req)
                {
                    var pmtdetailDto = new PmtdetailDto
                    {
                        Plu = reqPluno.Pluno
                    };

                    var pmtList = new List<PmtDto>();

                    if (tempPmtDtoList.Any(x => x.Pluno == reqPluno.Pluno)) // tempPmtDtoList符合品號數量 > 0
                    {
                        var tempPmtDto = tempPmtDtoList.Where(x => x.Pluno == reqPluno.Pluno).ToList();

                        for (int c = 0; c < reqPluno.Qty; c++)
                        {
                            pmtList.Add(new PmtDto()
                            {
                                Pmtno = tempPmtDto[c].Pmtno,
                                Pmtname = tempPmtDto[c].Pmtname,
                                Qty = tempPmtDto[c].Qty,
                                Discount = tempPmtDto[c].Discount,
                                Disrate = tempPmtDto[c].Disrate,
                            });

                            var promotion = permuteProductList.Where(x => x.Pluno == reqPluno.Pluno).Where(x => x.Qty > 0).First();
                            promotion.Qty -= 1;

                            if (promotion.Qty == 0)
                            {
                                tempPmtDtoList.Remove(tempPmtDtoList.Where(x => x.Pluno == reqPluno.Pluno).First());
                            }
                        }
                    }
                    else
                    {                       
                        for (int j = 0; j < permuteList.Count; j++)
                        {
                            if (_productListsDict.ContainsKey($"{index}_{j}"))
                            {
                                var productList = _productListsDict[$"{index}_{j}"];
                                var promotionQty = permuteProductList.Where(x => x.Pluno == reqPluno.Pluno).Sum(y=>y.Qty);

                                if (productList.Count == 0 || promotionQty == 0) //如果促銷名細為0或是剩下的促銷明細沒有該品號跳過
                                    continue;

                                var plunoList = productList.Where(x => x.Pluno == reqPluno.Pluno).ToList();
                                var permutePrice = _permutePriceListsDict[$"{index}_{j}"]; // 促銷組合售價/組數 = 單一促銷售價

                                // 促銷資料
                                var dataListString = await _cache.GetStringAsync("Promotion");
                                var dataList = JsonSerializer.Deserialize<List<PromotionDataDto>>(dataListString);
                                var pmtName = dataList!.SingleOrDefault(x => x.P_No == permuteList[j])!.P_Name;

                                //if (dataList!.SingleOrDefault(x => x.P_No == permuteList[j])!.Mix_Mode != "6") // 套餐促銷不用進入此function
                                //{
                                //    // 判斷每個品號的促銷價格加總是否等於最終促銷價格
                                //    if (productList.Sum(x => x.SalePrice * x.Qty) < permutePrice)
                                //    {
                                //        var maxPrice = productList.Max(x => x.Price); // 取得單品金額最大項

                                //        var remainderPrice = (permutePrice - productList.Sum(x => x.SalePrice)) / _countLists[index][j]; // 若有剩餘金額則攤平到各項目

                                //        for (int v = 0; v < _countLists[index][j]; v++)
                                //        {
                                //            productList[v].SalePrice = productList[v].SalePrice + remainderPrice;
                                //        }
                                //    }
                                //}

                                // 第一次進入迴圈時判斷每個品號的促銷價格加總是否等於最終促銷價格
                                if (nowIndex == 0 && productList.Sum(x => x.SalePrice * x.Qty) < permutePrice)
                                {
                                    var maxPrice = productList.Max(x => x.Price); // 取得單品金額最大項

                                    var remainderPrice = (permutePrice - productList.Sum(x => x.SalePrice * x.Qty)) / _countLists[index][j]; // 若有剩餘金額則攤平到各項目

                                    var maxPriceList = productList.Where(x => x.Price == maxPrice).ToList();

                                    for (int v = 0; v < maxPriceList.Count; v++)
                                    {
                                        maxPriceList[v].SalePrice = maxPriceList[v].SalePrice + remainderPrice;
                                    }
                                }

                                foreach (var pluno in plunoList)
                                {
                                    var pmt = new PmtDto();
                                    pmt.Pmtno = permuteList[j];
                                    pmt.Pmtname = pmtName;
                                    pmt.Qty = decimal.ToInt32(pluno.Qty);

                                    if (dataList!.SingleOrDefault(x => x.P_No == permuteList[j])!.Mix_Mode == "6") // 套餐促銷
                                    {
                                        pmt.Discount = decimal.ToInt32((pluno.Price * reqPluno.Qty) - (pluno.SalePrice * reqPluno.Qty) > 0 ? (pluno.Price * reqPluno.Qty) - (pluno.SalePrice * reqPluno.Qty) : 0);
                                    }
                                    else // 其他促銷
                                    {
                                        pmt.Discount = decimal.ToInt32((pluno.Price * pluno.Qty) - pluno.SalePrice > 0 ? (pluno.Price * pluno.Qty) - pluno.SalePrice : 0);
                                    }

                                    pmt.Disrate = pmt.Discount > 0 ? Math.Round((pluno.SalePrice * reqPluno.Qty) / (pluno.Price * reqPluno.Qty), 2) : 0; // 折扣率(四捨五入到小數點第二位)

                                    if (pmtList.Count < reqPluno.Qty)
                                    {
                                        pmtList.Add(pmt);
                                        var promotion = permuteProductList.Where(x => x.Pluno == reqPluno.Pluno).First();
                                        promotion.Qty -= 1;
                                    }
                                    else
                                    {
                                        // 多於組數加入到暫存
                                        var tempPmtDto = new TempPmtDto()
                                        {
                                            Pluno = reqPluno.Pluno,
                                            Pmtno = pmt.Pmtno,
                                            Pmtname = pmt.Pmtname,
                                            Qty = pmt.Qty,
                                            Discount = pmt.Discount,
                                            Disrate = pmt.Disrate
                                        };

                                        tempPmtDto.Pluno = reqPluno.Pluno;
                                        tempPmtDtoList.Add(tempPmtDto);
                                    }
                                }                                
                            }
                        }                        
                    }

                    pmtdetailDto.Saleprice = reqPluno.Qty * reqPluno.Price - pmtList.Sum(x => x.Discount);
                    pmtdetailDto.Pmt = pmtList;

                    pmtdetailList.Add(pmtdetailDto);

                    nowIndex++;
                }
            }
            else // 都不符合任一促銷
            {
                resultPrice = _totalPrice;

                foreach (var reqPluno in req)
                {
                    var pmtdetailDto = new PmtdetailDto
                    {
                        Plu = reqPluno.Pluno
                    };

                    pmtdetailList.Add(pmtdetailDto);
                }
            }

            return new GetPromotionPriceResp()
            {
                Totalprice = decimal.ToInt32(resultPrice),
                Pmtdetail = pmtdetailList,
            };
        }

        private async Task GetPmtDetailOnRedis(List<GetPromotionPriceReq> req)
        {
            var t0Sw = new Stopwatch();
            t0Sw.Start();

            var promotionMainDto = new PromotionMainDto()
            {
                Pmt123 = new List<PromotionDetailDto>(),
                Pmt45 = new List<PromotionDetailDto>(),
            };

            var inputPmtList = req.Select(x => x.Pluno).ToList(); // 購買商品品號

            foreach (var item in req)
            {
                var redisPromotionJson = await _cache.GetStringAsync(item.Pluno); // 取得Redis內此品號的促銷表

                if (redisPromotionJson == null)
                {
                    Console.WriteLine($"--無此商品: {item.Pluno} --");
                    return;
                }

                var redisPromotionDto = JsonSerializer.Deserialize<PromotionMainDto>(redisPromotionJson);

                foreach (var promotionDto in redisPromotionDto!.Pmt45)
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
                            var containRow45 = promotionDto.Combo.Where(x => inputPmtList.Contains(x.Pluno)); // 搜尋包含input的商品編號

                            if (containRow45.Any())
                            {
                                var mixPluMultipleDto = JsonSerializer.Deserialize<List<MixPluMultipleDto>>(await _cache.GetStringAsync(promotionDto.P_Key));
                                _mixPluMultipleDtoLists.AddRange(mixPluMultipleDto!);
                            }
                        }
                        else if (promotionDto.Mix_Mode == "3") // 變動分量以上
                        {

                        }
                        else if (promotionDto.Mix_Mode == "4") // 增量折扣
                        {
                            var containRow45 = promotionDto.Combo.Where(x => inputPmtList.Contains(x.Pluno)); // 搜尋包含input的商品編號

                            if (containRow45.Any())
                            {
                                var mixPluMultipleDto = JsonSerializer.Deserialize<List<MixPluMultipleDto>>(await _cache.GetStringAsync(promotionDto.P_Key));
                                _mixPluMultipleDtoLists.AddRange(mixPluMultipleDto!);
                            }
                        }
                        else if (promotionDto.Mix_Mode == "6") // 套餐組合
                        {
                            var containRow45 = promotionDto.Combo.Where(x => inputPmtList.Contains(x.Pluno)); // 搜尋包含input的商品編號

                            if (containRow45.Any())
                                promotionMainDto.Pmt45.Add(promotionDto);
                        }
                    }
                    else if (promotionDto.P_Type == PromotionType.Matching.Value()) // 配對搭贈
                    {
                        if (promotionDto.Mix_Mode == "1") // 固定組合
                        {
                            var containRow45 = promotionDto.Combo.Where(x => inputPmtList.Contains(x.Pluno)); // 搜尋包含input的商品編號

                            // 需同時符合A區和B區至少各一商品
                            if (containRow45.Any(x => x.Plu_Type == "1") && containRow45.Any(x => x.Plu_Type == "2"))
                            {
                                promotionMainDto.Pmt45.Add(promotionDto);
                            }
                        }
                    }
                }

                // 單品促銷
                if (redisPromotionDto.Pmt123.Count > 0)
                {
                    // 如果同品號有兩種單品促銷以上，選擇折扣率最大的單品促銷
                    var firstCombo = redisPromotionDto.Pmt123.Select(x => x.Combo.First()).OrderBy(y => y.Saleoff).First(); // 取得最大則扣率的Combo ( **每個PromotionDto只會有一個Combo** )
                    var firstPromotionDto = redisPromotionDto.Pmt123.Where(x => x.Combo.First() == firstCombo).First(); // 依據最大則扣率的Combo取得該筆PromotionDto
                    firstCombo.Qty = item.Qty;

                    var noContainRow123 = firstPromotionDto.Combo.Where(x => !inputPmtList.Contains(x.Pluno)); // 搜尋出不在此次input的商品編號
                    if (!noContainRow123.Any())
                        promotionMainDto.Pmt123.Add(firstPromotionDto);
                }
            }

            promotionMainDto.Pmt45 = promotionMainDto.Pmt45.Distinct(x => x.P_No).ToList(); // 過濾重複組合促銷
            _mixPluMultipleDtoLists = _mixPluMultipleDtoLists.DistinctBy(x => new { x.A_No, x.P_Type, x.P_No, x.Seq }).ToList(); // 過濾重複變動分量組合促銷

            // 取得所有符合條件的促銷代號
            var pNoList = promotionMainDto.Pmt123.Select(x => x.P_No).Union(promotionMainDto.Pmt45.Select(x => x.P_No).Union(_mixPluMultipleDtoLists.Select(x => x.P_No))).ToList();

            t0Sw.Stop();

            var t3Sw = new Stopwatch();
            t3Sw.Start();

            // 計算促銷的排列組合
            _permuteLists = PermutationsUtil.Permute(pNoList);
            t3Sw.Stop();

            var t4Sw = new Stopwatch();
            t4Sw.Start();

            // 計算各排列組合商品數量
            _countLists = await GetPermuteCount(req, promotionMainDto);
            t4Sw.Stop();

            var t5Sw = new Stopwatch();
            t5Sw.Start();
            // 計算各排列組合價錢
            _priceList = await GetPermutePrice(promotionMainDto, req);
            t5Sw.Stop();

            Console.WriteLine($"找組數耗時 : {t0Sw.ElapsedMilliseconds} 豪秒");
            Console.WriteLine($"計算促銷的排列組合耗時 : {t3Sw.ElapsedMilliseconds} 豪秒");
            Console.WriteLine($"計算各排列組合商品數量耗時 : {t4Sw.ElapsedMilliseconds} 豪秒");
            Console.WriteLine($"計算各排列組合價錢耗時 : {t5Sw.ElapsedMilliseconds} 豪秒");

            // 印出排列組合 & 組合數量 & 價錢
            PrintResult(req);
        }

        /// <summary>
        /// 印出促銷排列組合、促銷組數
        /// </summary>
        /// <param name="permuteLists">促銷排列組合</param>
        /// <param name="countLists">促銷組數</param>
        /// <param name="req">Request Input</param>
        private void PrintResult(List<GetPromotionPriceReq> req)
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
                var math = Math.Round(decimal.ToInt32(_priceList[i]) / _totalPrice, 2); // 折扣率(四捨五入到小數點第二位)
                Console.WriteLine($"    [{string.Join(',', _permuteLists[i])}] ({string.Join(',', _countLists[i])}) (原價:{_totalPrice} 折:{_totalPrice - Decimal.ToInt32(_priceList[i])} 折扣率:{math} 實際銷售金額:{Decimal.ToInt32(_priceList[i])} )");
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
                                consoleString += $"{product.Pluno} * {decimal.ToInt16(product.Qty)} Price={product.SalePrice}, ";
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
                var promotionCountList = new List<int>(); // 排列組合數量列表

                // 複製req當作計算扣除組合促銷後的剩餘商品數量
                var copyReq = await GetInputReq(req);

                // 取得單一組合促銷方案(Pmt45)
                for (int j = 0; j < _permuteLists[i].Count; j++)
                {
                    var subProductList = new List<ProductDetailDto>(); // 促銷品號明細
                    var pmt45List = promotionMainDto.Pmt45.Where(x => x.P_No == _permuteLists[i][j]);
                    var pmt123List = promotionMainDto.Pmt123.Where(x => x.P_No == _permuteLists[i][j]);

                    if (pmt45List.Any()) // 固定組合數量>0
                    {
                        var pmt45ComboList = pmt45List.First().Combo;

                        // 扣除商品數量&計算組數
                        // 取得input商品中符合組合促銷的商品組合
                        var curPromotionList = copyReq.Where(x => pmt45ComboList.Select(y => y.Pluno).Contains(x.Pluno)).ToList();
                        var promotion45Count = 0; // 組合促銷組數

                        if (pmt45List.First().P_Type == "4") // 組合品搭贈
                        {
                            if (pmt45List.First().Mix_Mode == "6") // 促銷套餐
                            {
                                var mealProductList = new List<ProductDetailDto>();
                                var mealGroupCount = new List<decimal>(); // 符合套餐數量
                                var mealGroupPluno = new List<List<string>>(); // 符合套餐品號

                                foreach (var pmt45Combo in pmt45ComboList)
                                {
                                    var mealPlunoList = pmt45Combo.Pluno.Split(",");
                                    var mealGroupPlunoArray = new List<string>();

                                    decimal mealCount = 0;
                                    foreach (var mealPluno in mealPlunoList)
                                    {
                                        var reqPluno = copyReq.Where(x => x.Pluno == mealPluno).FirstOrDefault();

                                        if (reqPluno != null)
                                        {
                                            mealCount += reqPluno.Qty;

                                            for (var k = 0; k < reqPluno.Qty; k++)
                                            {
                                                mealGroupPlunoArray.Add(reqPluno.Pluno);
                                            }
                                        }
                                    }

                                    // 紀錄套餐Group數量
                                    mealGroupCount.Add((decimal)((mealCount / pmt45Combo.Match) < 1 ? 0 : (mealCount / pmt45Combo.Match)));

                                    // 記錄套餐品號
                                    mealGroupPluno.Add(mealGroupPlunoArray);
                                }

                                if (!mealGroupCount.Contains(0)) // 套餐成立(Match數量沒有為0的)
                                {
                                    // 增加最小促銷組數
                                    promotion45Count += decimal.ToInt32(mealGroupCount.Min());

                                    for (int o = 0; o < pmt45ComboList.Count; o++)
                                    {
                                        var matchCount = decimal.ToInt32((decimal)(pmt45ComboList[o].Match * mealGroupCount.Min()));

                                        mealGroupPluno[o].RemoveRange(matchCount, mealGroupPluno[o].Count - matchCount);
                                    }

                                    foreach (var groupPluno in mealGroupPluno)
                                    {
                                        foreach (var pluno in groupPluno)
                                        {
                                            var reqPluno = copyReq.Where(x => x.Pluno == pluno).First();

                                            var mealProduct = mealProductList.Where(x => x.Pluno == pluno).FirstOrDefault();

                                            if (mealProduct == null)
                                            {
                                                mealProductList.Add(new ProductDetailDto
                                                {
                                                    Pluno = pluno,
                                                    Qty = 1,
                                                    Price = reqPluno.Price,
                                                });
                                            }
                                            else
                                            {
                                                mealProduct.Qty++;
                                            }

                                            reqPluno.Qty--;
                                        }
                                    }

                                    // 新增此促銷商品明細
                                    await SubProductListAddProd(ref subProductList, mealProductList);
                                }
                            }
                            else
                            {
                                while (curPromotionList.Select(x => x.Qty).All(y => y > 0)) // 如果輸入商品組合數量都不為0就多新增一組商品組合並扣除數量
                                {
                                    var mixCount = 0;
                                    foreach (var promotionPrice in curPromotionList)
                                    {
                                        if (promotionPrice.Qty == 0)
                                            break;

                                        var promotion = copyReq.Where(x => x.Pluno == promotionPrice.Pluno).First();
                                        var qty = pmt45ComboList.Where(x => x.Pluno == promotionPrice.Pluno).First().Qty;
                                        promotion.Qty -= qty;

                                        // 新增此促銷商品明細
                                        await SubProductListAddProd(ref subProductList, promotion.Pluno, qty, promotion.Price);

                                        mixCount++;

                                        if (mixCount == 2)
                                            break;
                                    }

                                    // 促銷組數+1 
                                    promotion45Count++;
                                }
                            }                            
                        }
                        else if (pmt45List.First().P_Type == "5") // 配對搭贈
                        {
                            var redisPromotionJson = await _cache.GetStringAsync(pmt45List.First().P_Key);
                            var redisPromotionDto = JsonSerializer.Deserialize<FmMixAbpluDetailDto>(redisPromotionJson);
                            var pluType1List = redisPromotionDto!.Detail.Where(x => x.Plu_Type == "1").ToList();
                            var pluType2List = redisPromotionDto.Detail.Where(x => x.Plu_Type == "2").ToList();

                            foreach (var promotion in curPromotionList)
                            {
                                if (promotion.Qty == 0)
                                    continue;

                                var promotionPluType = redisPromotionDto.Detail.Where(x => x.Pluno == promotion.Pluno).First().Plu_Type;
                                var subPromotionList = new List<GetPromotionPriceReq>();

                                if (promotionPluType == "1")
                                {
                                    subPromotionList = copyReq.Where(x => pluType2List.Select(y => y.Pluno).Contains(x.Pluno)).ToList();
                                }
                                else if (promotionPluType == "2")
                                {
                                    subPromotionList = copyReq.Where(x => pluType1List.Select(y => y.Pluno).Contains(x.Pluno)).ToList();
                                }

                                if (subPromotionList.Count > 0)
                                {
                                    var subPromotion = subPromotionList.Where(x => x.Qty > 0).FirstOrDefault(); // 取得搭配商品
                                    if (subPromotion != null)
                                    {
                                        promotion.Qty--;
                                        subPromotion.Qty--;

                                        // 促銷組數+1 
                                        promotion45Count++;

                                        // 主要商品
                                        // 新增此促銷商品明細
                                        await SubProductListAddProd(ref subProductList, promotion.Pluno, 1, promotion.Price);

                                        // 搭配商品
                                        // 新增此促銷商品明細
                                        await SubProductListAddProd(ref subProductList, subPromotion.Pluno, 1, subPromotion.Price);
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }
                                else
                                {
                                    continue;
                                }
                            }
                        }                        

                        promotionCountList.Add(promotion45Count);
                    }
                    else if (pmt123List.Any())
                    {
                        var pmt123ComboList = pmt123List.First().Combo;
                        var curPromotionList = copyReq.Where(x => pmt123ComboList.Select(y => y.Pluno).Contains(x.Pluno)).ToList();

                        var comboList = new List<ComboDto>();
                        var pmt123DtoList = promotionMainDto.Pmt123.Where(x => x.P_Type == pmt123List.First().P_Type).ToList();

                        foreach (var pmt123Dto in pmt123DtoList)
                        {
                            comboList.AddRange(pmt123Dto.Combo);
                        }

                        // 符合單品促銷
                        var promotion123Count = comboList.Sum(x => x.Qty); // 單品促銷組數

                        foreach (var combo in comboList)
                        {
                            var promotion = copyReq.Where(x => x.Pluno == combo.Pluno).First();
                            promotion.Qty -= combo.Qty;

                            await SubProductListAddProd(ref subProductList, combo.Pluno, combo.Qty, promotion.Price);
                        }

                        promotionCountList.Add(decimal.ToInt32(promotion123Count));

                    }
                    else // 變動分量組合
                    {
                        var mixPluMultipleDtoList = _mixPluMultipleDtoLists.Where(x => x.P_No == _permuteLists[i][j]).OrderByDescending(x => x.Seq).ToList();

                        if (mixPluMultipleDtoList.Count == 0) // 不符合組合促銷跳過
                            break;

                        var plunoList = mixPluMultipleDtoList.Select(x => x.PlunoList).First();

                        // 扣除商品數量&計算組數
                        // 取得input商品中符合組合促銷的商品組合
                        var curPromotionList = copyReq.Where(x => plunoList.Select(y => y).Contains(x.Pluno)).ToList();
                        var promotionMultiCount = 0; // 促銷變動分量組數
                        var minModQty = mixPluMultipleDtoList[^1].Mod_Qty; //此促銷變動分量的最小組數

                        var dKey = $"{mixPluMultipleDtoList[0].A_No}{mixPluMultipleDtoList[0].P_Type}{mixPluMultipleDtoList[0].P_No}{i}{j}"; // _multipleCountDict Key
                        var multipleCountDtoList = new List<MultipleCountDto>();

                        var promotionList = new List<string>(); // 依照傳入順序排列品項

                        foreach (var promotion in curPromotionList)
                        {
                            for (int c = 0; c < promotion.Qty; c++)
                            {
                                promotionList.Add(promotion.Pluno);
                            }
                        }

                        decimal sumCount = promotionList.Count;
                        int index = 0;

                        while (sumCount >= minModQty)
                        {
                            foreach (var mixPluMultipleDto in mixPluMultipleDtoList)
                            {
                                if (sumCount < mixPluMultipleDto.Mod_Qty) // 剩餘數量小於最小組數時跳出迴圈
                                {
                                    await MultipleCountDtoListAddProd(ref multipleCountDtoList, mixPluMultipleDto.Seq, 0);

                                    continue;
                                }
                                else
                                {
                                    await MultipleCountDtoListAddProd(ref multipleCountDtoList, mixPluMultipleDto.Seq, 1);

                                    if (mixPluMultipleDto.Mix_Mode == "4") // 增量折扣
                                    {
                                        if (mixPluMultipleDto.Is_Same_Plu == "Y") // 1:同品項
                                        {
                                            foreach (var promotion in curPromotionList)
                                            {
                                                var reqPluno = copyReq.Where(x => x.Pluno == promotion.Pluno).First();

                                                if (promotion.Qty >= mixPluMultipleDto.Mod_Qty)
                                                {
                                                    var mathResult = Math.Floor(promotion.Qty / mixPluMultipleDto.Mod_Qty);

                                                    for (int c = 0; c < mathResult * mixPluMultipleDto.Mod_Qty; c++)
                                                    {
                                                        // 新增此促銷商品明細
                                                        await SubProductListAddProd(ref subProductList, promotion.Pluno, 1, promotion.Price);
                                                    }

                                                    sumCount -= promotion.Qty;
                                                    reqPluno.Qty = promotion.Qty % mixPluMultipleDto.Mod_Qty;

                                                    // 增加促銷組數 
                                                    promotionMultiCount += decimal.ToInt32(mathResult);
                                                }
                                                else
                                                {
                                                    sumCount -= promotion.Qty;
                                                }
                                            }
                                        }
                                        else if (mixPluMultipleDto.Is_Same_Plu == "N") // 2:不同品項
                                        {
                                            foreach (var promotion in curPromotionList)
                                            {
                                                var promotionNumber = promotion.Qty;
                                                var reqPluno = copyReq.Where(x => x.Pluno == promotion.Pluno).First(); // 主要商品
                                                var ortherReqPlunoList = copyReq.Where(x => x.Pluno != promotion.Pluno).ToList(); // 搭配商品列表

                                                for (int c = 0; c < promotionNumber; c++)
                                                {
                                                    if (1 + ortherReqPlunoList.Sum(x => x.Qty) >= mixPluMultipleDto.Mod_Qty) // 主要商品1個 + 搭配商品總數量 > 最低組數
                                                    {
                                                        foreach (var ortherReqPluno in ortherReqPlunoList)
                                                        {
                                                            if (ortherReqPluno.Qty == 0)
                                                                continue;

                                                            // 主要商品
                                                            // 新增此促銷商品明細
                                                            await SubProductListAddProd(ref subProductList, promotion.Pluno, 1, reqPluno.Price);

                                                            reqPluno.Qty--;
                                                            sumCount--; // 扣除主商品數量

                                                            for (int k = 0; k < mixPluMultipleDto.Mod_Qty - 1; k++)
                                                            {
                                                                // 搭配商品
                                                                // 新增此促銷商品明細
                                                                await SubProductListAddProd(ref subProductList, ortherReqPluno.Pluno, 1, ortherReqPluno.Price);

                                                                ortherReqPluno.Qty--;
                                                                sumCount--; // 扣除搭配商品數量
                                                            }

                                                            // 增加促銷組數 
                                                            promotionMultiCount++;
                                                            break;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        sumCount = sumCount - promotion.Qty;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        var mathResult = Math.Floor(sumCount / mixPluMultipleDto.Mod_Qty);
                                        sumCount %= mixPluMultipleDto.Mod_Qty;

                                        for (int c = 0; c < mathResult * mixPluMultipleDto.Mod_Qty; c++)
                                        {
                                            var promotion = copyReq.Where(x => x.Pluno == promotionList[index]).First();
                                            promotion.Qty--;

                                            // 新增此促銷商品明細
                                            await SubProductListAddProd(ref subProductList, promotionList[index], 1, promotion.Price);

                                            index++;
                                        }

                                        // 增加促銷組數 
                                        promotionMultiCount += decimal.ToInt32(mathResult);
                                    }
                                }
                            }
                        }

                        _multipleCountDict.Add(dKey, multipleCountDtoList);

                        promotionCountList.Add(promotionMultiCount);
                    }

                    _productListsDict.Add($"{i}_{j}", subProductList); // 記錄此促銷扣除的品號及數量
                }

                // 若input商品還有剩，則計算單品促銷(Pmt123)
                if (copyReq.Select(x => x.Qty).Any(y => y > 0))
                {
                    var subProductList = new List<ProductDetailDto>(); // 促銷品號明細
                    var remainProductList = new List<GetPromotionPriceReq>();

                    foreach (var item in copyReq)
                    {
                        if (item.Qty == 0) // 數量為0跳過
                            continue;

                        remainProductList.Add(item);
                    }
                                        
                    _remainProductListsDict.Add($"{i}", remainProductList);
                }

                countLists.Add(promotionCountList);
            }

            return countLists;
        }

        /// <summary>
        /// 計算促銷組合價錢
        /// </summary>
        /// <returns></returns>
        private async Task<IList<decimal>> GetPermutePrice(PromotionMainDto promotionMainDto, List<GetPromotionPriceReq> req)
        {
            IList<decimal> priceLists = new List<decimal>();

            if (_permuteLists.Count > 0)
            {
                // 取得每一組排列組合
                for (int i = 0; i < _permuteLists.Count; i++)
                {
                    decimal salePrice = 0; // 實際銷售金額

                    // 複製req當作計算扣除組合促銷後的剩餘商品數量
                    var copyReq = await GetInputReq(req);

                    for (int j = 0; j < _permuteLists[i].Count; j++)
                    {
                        var permuteDetail = _productListsDict[$"{i}_{j}"]; // 促銷組合品項明細
                        var pmt45 = promotionMainDto.Pmt45.Where(x => x.P_No == _permuteLists[i][j]).FirstOrDefault();
                        decimal permutePrice = 0;

                        if (_productListsDict.ContainsKey($"{i}_{j}"))
                        {                            
                            if (pmt45 != null) // 取得組合品促銷方案(固定組合 mix_mode=1)價錢
                            {
                                permutePrice = pmt45.SalePrice * _countLists[i][j];

                                // 計算扣除組合促銷後的剩餘商品數量                      
                                foreach (var permute in permuteDetail)
                                {
                                    var promotion = copyReq.Where(x => x.Pluno == permute.Pluno).First();
                                    promotion.Qty -= permute.Qty;
                                    permute.SalePrice = Math.Floor(permutePrice * promotion.Price / permuteDetail.Sum(x => x.Price * x.Qty));
                                }
                            }
                            else // mix_mode!=1
                            {
                                var mixPluMultipleDtoList = _mixPluMultipleDtoLists.Where(x => x.P_No == _permuteLists[i][j]).ToList();

                                if (mixPluMultipleDtoList.Count > 0) // 取得組合品促銷方案(變動分量組合)價錢
                                {
                                    var firstMixPluMultipleDto = mixPluMultipleDtoList.First(); // 取得第一筆mixPluMultipleDto

                                    var dKey = $"{firstMixPluMultipleDto.A_No}{firstMixPluMultipleDto.P_Type}{firstMixPluMultipleDto.P_No}{i}{j}"; // _multipleCountDict Key
                                    var multipleCountDtoList = _multipleCountDict[dKey]; //取得組合數量

                                    if (firstMixPluMultipleDto.P_Mode == "1" & multipleCountDtoList.Count > 0) // 特價
                                    {
                                        foreach (var mixPluMultipleDto in mixPluMultipleDtoList)
                                        {
                                            permutePrice += firstMixPluMultipleDto.No_Vip_Saleprice;
                                        }

                                        permutePrice = permutePrice * _countLists[i][j]; // (價錢 * 組數)

                                        // 計算促銷後的商品價格                      
                                        foreach (var permute in permuteDetail)
                                        {
                                            var promotion = copyReq.Where(x => x.Pluno == permute.Pluno).First();
                                            permute.SalePrice = Math.Floor(permutePrice * promotion.Price / permuteDetail.Sum(x => x.Price * x.Qty));
                                        }
                                    }
                                    else if (firstMixPluMultipleDto.P_Mode == "2" & multipleCountDtoList.Count > 0) // 折扣
                                    {
                                        var reqPlunoList = copyReq.Where(x => firstMixPluMultipleDto.PlunoList.Contains(x.Pluno)).ToList();
                                        decimal totalPrice = 0; // 總金額
                                        decimal tempPrice = 0; // 折扣價格

                                        foreach (var mixPluMultipleDto in mixPluMultipleDtoList)
                                        {
                                            // 計算總金額

                                            if (mixPluMultipleDto.Mix_Mode == "2") // 變動分量組合
                                            {
                                                foreach (var permute in permuteDetail)
                                                {
                                                    totalPrice += permute.Qty * permute.Price;

                                                    var promotion = copyReq.Where(x => x.Pluno == permute.Pluno).First();
                                                    promotion.Qty -= permute.Qty;
                                                    permute.SalePrice = Math.Floor(permute.Price * mixPluMultipleDto.No_Vip_Saleoff); // 折扣價格,採用無條件捨去
                                                }

                                                tempPrice += Math.Floor(totalPrice * mixPluMultipleDto.No_Vip_Saleoff); // 折扣價格 (總金額 * 折扣率) 採用無條件捨去
                                            }
                                            else if (mixPluMultipleDto.Mix_Mode == "4") // 增量折扣
                                            {
                                                decimal index = 0;

                                                // 計算扣除組合促銷後的剩餘商品數量                        
                                                foreach (var permute in permuteDetail)
                                                {
                                                    index++;

                                                    if ((index % mixPluMultipleDto.Mod_Qty) == 0) // 餘數為0表示為第n件需計算折扣
                                                    {
                                                        var mathResult = Math.Floor(permute.Price * mixPluMultipleDto.No_Vip_Saleoff);
                                                        tempPrice += mathResult; // 第n件n折 ( 商品價錢 * 折扣%數 ) 採用無條件捨去
                                                        permute.SalePrice = mathResult;
                                                    }
                                                    else
                                                    {
                                                        tempPrice += permute.Price;
                                                        permute.SalePrice = permute.Price;
                                                    }
                                                }
                                            }
                                        }

                                        permutePrice = tempPrice;
                                    }
                                    else if (firstMixPluMultipleDto.P_Mode == "3" & multipleCountDtoList.Count > 0) // 折價
                                    {
                                        decimal discount = 0; //變動分量組合折價金額
                                        decimal discountPrice = 0; // 折價金額

                                        var minModQty = mixPluMultipleDtoList[^1].Mod_Qty; //最小組數
                                        var sumCount = permuteDetail.Sum(x => x.Qty);

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
                                                    var pCount = multipleCountDtoList.Where(x => x.PSeq == mixPluMultipleDto.Seq).First().PCount; // 組合數量
                                                    discount = pCount * mixPluMultipleDto.No_Vip_Amount; // 折價金額 (組數 * 折扣金額)

                                                    var result = Math.Floor(sumCount / mixPluMultipleDto.Mod_Qty); // 購買商品符合的組數
                                                    sumCount = sumCount % mixPluMultipleDto.Mod_Qty; // 剩餘商品數量

                                                    discountPrice += discount * result; // 總折價價格 (折價價格 * 符合組數)

                                                    // 計算促銷後的商品價格                      
                                                    foreach (var permute in permuteDetail)
                                                    {
                                                        var promotion = copyReq.Where(x => x.Pluno == permute.Pluno).First();
                                                        permute.SalePrice = Math.Floor(promotion.Price - discountPrice / permuteDetail.Sum(x=>x.Qty));
                                                    }
                                                }
                                            }
                                        }

                                        // 計算促銷金額
                                        permutePrice = permuteDetail.Sum(x => x.Price * x.Qty) - discountPrice;
                                    }
                                }
                                else
                                {
                                    var pmt123 = promotionMainDto.Pmt123.Where(x => x.P_No == _permuteLists[i][j]);

                                    if (pmt123.Any())// 取得單品促銷方案價錢
                                    {
                                        var pmt123Combo = pmt123.First().Combo.First();
                                        var reqPluno = copyReq.Where(x => x.Pluno == pmt123Combo.Pluno).First();
                                        permutePrice = Math.Floor((decimal)(pmt123Combo.Saleoff * reqPluno.Price)) * _countLists[i][j]; // (價錢 * 組數)

                                        foreach (var permute in permuteDetail)
                                        {
                                            var promotion = copyReq.Where(x => x.Pluno == reqPluno.Pluno).First();
                                            promotion.Qty -= permute.Qty;
                                            permute.SalePrice = Math.Floor((decimal)(pmt123Combo.Saleoff * promotion.Price)) * permute.Qty;
                                        }
                                    }
                                }
                            }

                            salePrice += permutePrice;
                            _permutePriceListsDict.Add($"{i}_{j}", permutePrice);
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

                    priceLists.Add(salePrice);
                }
            }
            else // 輸入商品都沒有符合一筆促銷
            {
                var remainProductList = new List<GetPromotionPriceReq>();

                var reqPlune = req.Where(x => x.Qty > 0).ToList();
                remainProductList.AddRange(reqPlune);

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

        /// <summary>
        /// 複製一組Request Payload
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        private Task<List<GetPromotionPriceReq>> GetInputReq(List<GetPromotionPriceReq> req)
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

            return Task.FromResult(copyReq);
        }

        /// <summary>
        /// 新增此促銷商品明細
        /// </summary>
        /// <param name="subProductList">商品明細列表</param>
        /// <param name="pluno">品號</param>
        /// <param name="qty">數量</param>
        /// <param name="price">價錢</param>
        /// <returns></returns>
        private Task SubProductListAddProd(ref List<ProductDetailDto> subProductList, string pluno, decimal qty, decimal price)
        {
            subProductList.Add(new ProductDetailDto
            {
                Pluno = pluno,
                Qty = qty,
                Price = price,
            });
            
            return Task.FromResult(subProductList);
        }

        /// <summary>
        /// 新增此促銷商品明細
        /// </summary>
        /// <param name="subProductList">商品明細列表</param>
        /// <param name="pluno">品號</param>
        /// <param name="qty">數量</param>
        /// <param name="price">價錢</param>
        /// <returns></returns>
        private Task SubProductListAddProd(ref List<ProductDetailDto> subProductList, List<ProductDetailDto> mealProductList)
        {
            subProductList.AddRange(mealProductList);

            return Task.FromResult(subProductList);
        }


        private Task MultipleCountDtoListAddProd(ref List<MultipleCountDto> multipleCountDtoList, int seq, decimal count)
        {
            multipleCountDtoList.Add(new MultipleCountDto()
            {
                PSeq = seq,
                PCount = count
            });

            return Task.FromResult(multipleCountDtoList);
        }

        private Task<List<ProductDetailDto>> GetAllPermuteProductList(IList<string> permuteList, int index)
        {
            var productDetailList = new List<ProductDetailDto>();
            for (int j = 0; j < permuteList.Count; j++)
            {
                if (_productListsDict.ContainsKey($"{index}_{j}"))
                {
                    var productList = _productListsDict[$"{index}_{j}"];

                    if (productList.Count == 0)
                        continue;

                    productDetailList.AddRange(productList);
                }
            }

            return Task.FromResult(productDetailList);
        }
    }
}
