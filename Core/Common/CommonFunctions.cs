using Microsoft.Extensions.DependencyInjection;

namespace Core.Common
{
    public static class CommonFunctions
    {

        public static string ReplaceTr(string text)
        {
            text = text.Trim();

            string[] trkChars = { "Ç", "ç", "Ğ", "ğ", "ı", "İ", "Ö", "ö", "Ş", "ş", "Ü", "ü" };
            string[] engChars = { "C", "c", "G", "g", "i", "I", "O", "o", "S", "s", "U", "u" };

            for (int i = 0; i < trkChars.Length; i++)
            {
                text = text.Replace(trkChars[i], engChars[i]);
            }
            return text;
        }

        ///MZK Not: Bu metot Common Function a çekilebilir.
        public static string GetErrorMessage(string errorCode)
        {
            var errorMessages = new Dictionary<string, string>
                {
                    { "1", "Malzeme yok!" },
                    { "2", "Ürün sipariş numarası yok!" },
                    { "3", "Malzeme uygun formatta değil!" },
                    { "5", "Malzeme bulunamadı!" },
                    { "6", "Ürün sipariş numarası bulunamadı!" },
                    { "7", "Ürün sipariş numarası bu malzemeye ait değil!" },
                };
            return errorMessages.TryGetValue(errorCode, out var message) ? message : "Bilinmeyen hata";
        }
        public static string  TrToEng(string sqlStr)
        {
            string turkceKarakter = "ığüşöçĞÜŞİÖÇ";
            string karsiKarakter = "igusocGUSIOC";
            bool temp2 = false;
            string temp = string.Empty;

            for (int i = 0; i < sqlStr.Length; i++)
            {
                string temp1 = sqlStr[i].ToString();

                if (temp1 == "'")
                {
                    temp2 = !temp2;
                }

                if (!temp2)
                {
                    int j = turkceKarakter.IndexOf(temp1);
                    if (j != -1)
                    {
                        temp1 = karsiKarakter[j].ToString();
                    }
                }

                temp += temp1;
            }

            return temp;
        }

    }
}
