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

        public static string TrToEng(string sqlStr)
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
