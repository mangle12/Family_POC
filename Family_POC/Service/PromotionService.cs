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

                    // 利用主鍵搜尋組合商品
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

            var 促銷排列組合 = PermutationsUtil.Permute(promotionMainDto.Pmt45.Select(x => x.P_No).ToList());            
            PrintResult(促銷排列組合);

            int b =0;
        }

        static void PrintResult(IList<IList<string>> lists)
        {
            Console.WriteLine("[");
            foreach (var list in lists)
            {
                Console.WriteLine($"    [{string.Join(',', list)}]");
            }
            Console.WriteLine("]");
        }

    }
}
