using System.ComponentModel;

namespace Family_POC.Extension
{
    public static class EnumExtension
    {
        /// <summary>
        /// 取得屬性值
        /// </summary>
        /// <typeparam name="TAttribute">Attribute</typeparam>
        /// <param name="enumObj">外部enum</param>
        /// <returns></returns>
        public static TAttribute GetAttributeText<TAttribute>(this System.Enum enumObj) where TAttribute : Attribute
        {
            var attr = enumObj.GetType()
                .GetField(enumObj.ToString())
                .GetCustomAttributes(typeof(TAttribute), false)
                .First() as TAttribute;

            return attr;
        }

        public static string Value(this System.Enum enumObj)
        {
            return enumObj.ToString("d");
        }

        /// <summary>
        /// 列舉數值 to 列舉項目
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public static T StringToEnum<T>(this string value)
        {
            return (T)System.Enum.Parse(typeof(T), value);
        }

        /// <summary>
        /// 取得列舉描述名稱
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string GetEnumDescription(this System.Enum value)
        {
            System.Reflection.FieldInfo fi = value.GetType().GetField(value.ToString());

            DescriptionAttribute[] attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);

            if (attributes != null && attributes.Length > 0)
                return attributes[0].Description;
            else
                return value.ToString();
        }
    }
}
