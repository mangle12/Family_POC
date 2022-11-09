namespace Family_POC.Util
{
    public class PermutationsUtil
    {
        public static IList<IList<string>> Permute(List<string> inputList)
        {
            var list = new List<IList<string>>();

            return DoPermute(inputList.ToArray(), 0, inputList.Count - 1, list);
        }

        /// <summary>
        /// 排列組合
        /// </summary>
        /// <param name="inputList"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="list"></param>
        /// <returns></returns>
        static IList<IList<string>> DoPermute(string[] inputList, int start, int end, IList<IList<string>> list)
        {
            if (start == end)
            {
                list.Add(new List<string>(inputList));
            }
            else
            {
                for (var i = start; i <= end; i++)
                {
                    Swap(ref inputList[start], ref inputList[i]);
                    DoPermute(inputList, start + 1, end, list);
                    Swap(ref inputList[start], ref inputList[i]);
                }
            }

            return list;
        }

        static void Swap(ref string a, ref string b)
        {
            var temp = a;
            a = b;
            b = temp;
        }
    }
}
