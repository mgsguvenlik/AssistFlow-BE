using Core.Utilities.Constants;
using System.Globalization;

namespace Core.Common
{
    public static class CommonFunctions
    {

        public static string ReplaceTr(string text)
        {
            text = text.Trim();

            string[] trkChars = CommonConstants.TrChars;
            string[] engChars = CommonConstants.EngChars;

            for (int i = 0; i < trkChars.Length; i++)
            {
                text = text.Replace(trkChars[i], engChars[i]);
            }
            return text;
        }

        public static string TrToEng(string sqlStr)
        {
            string turkceKarakter = CommonConstants.TurkishCharacters;
            string karsiKarakter = CommonConstants.AsciiEquivalents;
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

        public static HashSet<string> ParseRoleCodes(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            return raw
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static string NormalizeEnumSearchText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim()
                .ToLowerInvariant()
                .Replace("ı", "i")
                .Replace("ğ", "g")
                .Replace("ü", "u")
                .Replace("ş", "s")
                .Replace("ö", "o")
                .Replace("ç", "c");
        }

        public static List<TEnum> MatchEnumValues<TEnum>(
            string search,
            IReadOnlyDictionary<TEnum, string[]>? aliases = null)
            where TEnum : struct, Enum
        {
            var normalizedSearch = NormalizeEnumSearchText(search);
            var result = new List<TEnum>();

            foreach (var value in Enum.GetValues<TEnum>())
            {
                var enumName = NormalizeEnumSearchText(value.ToString());
                var enumNumber = Convert.ToInt64(value, CultureInfo.InvariantCulture)
                    .ToString(CultureInfo.InvariantCulture);

                var aliasMatch = false;

                if (aliases != null && aliases.TryGetValue(value, out var aliasList))
                {
                    aliasMatch = aliasList.Any(alias =>
                    {
                        var normalizedAlias = NormalizeEnumSearchText(alias);
                        return normalizedAlias.Contains(normalizedSearch)
                               || normalizedSearch.Contains(normalizedAlias);
                    });
                }

                if (enumName.Contains(normalizedSearch)
                    || enumNumber == normalizedSearch
                    || aliasMatch)
                {
                    result.Add(value);
                }
            }

            return result.Distinct().ToList();
        }
    }
}
