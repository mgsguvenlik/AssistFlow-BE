using Model.Concrete;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Business.Utilities
{
    /// <summary>
    /// Reads both the current User JSON contract and archive records written with
    /// the legacy Technician-prefixed property names.
    /// </summary>
    public static class ArchiveUserJsonCompatibility
    {
        public static User? Deserialize(string? json)
        {
            if (string.IsNullOrWhiteSpace(json) || string.Equals(json.Trim(), "null", StringComparison.OrdinalIgnoreCase))
                return null;

            try
            {
                var user = JsonConvert.DeserializeObject<User>(json);
                if (user is null)
                    return null;

                var jsonObject = JObject.Parse(json);

                user.Code = WithLegacyFallback(user.Code, ReadString(jsonObject, "TechnicianCode")) ?? string.Empty;
                user.Company = WithLegacyFallback(user.Company, ReadString(jsonObject, "TechnicianCompany"));
                user.Address = WithLegacyFallback(user.Address, ReadString(jsonObject, "TechnicianAddress"));
                user.Name = WithLegacyFallback(user.Name, ReadString(jsonObject, "TechnicianName")) ?? string.Empty;
                user.Phone = WithLegacyFallback(user.Phone, ReadString(jsonObject, "TechnicianPhone"));
                user.Email = WithLegacyFallback(user.Email, ReadString(jsonObject, "TechnicianEmail"));

                return user;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? ReadString(JObject jsonObject, string propertyName)
        {
            var token = jsonObject.GetValue(propertyName, StringComparison.OrdinalIgnoreCase);
            return token is null || token.Type == JTokenType.Null ? null : token.Value<string>();
        }

        private static string? WithLegacyFallback(string? currentValue, string? legacyValue)
            => string.IsNullOrWhiteSpace(currentValue) ? legacyValue : currentValue;
    }
}
