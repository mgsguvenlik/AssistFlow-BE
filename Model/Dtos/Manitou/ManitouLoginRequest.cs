using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Model.Dtos.Manitou
{
   
        public sealed class ManitouLoginRequest
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            [JsonPropertyName("password")]
            public string Password { get; set; } = "";

            [JsonPropertyName("authenticationType")]
            public string AuthenticationType { get; set; } = "manitou_contact";

            [JsonPropertyName("acceptedEula")]
            public bool AcceptedEula { get; set; } = false;

            [JsonPropertyName("newPassword")]
            public string NewPassword { get; set; } = "";
        }

        public sealed class ManitouLoginResponse
        {
            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }

            [JsonPropertyName("token_type")]
            public string? TokenType { get; set; }

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonPropertyName("error")]
            public string? Error { get; set; }

            [JsonPropertyName("error_description")]
            public string? ErrorDescription { get; set; }
        }

        public sealed class ManitouSearchRequestItem
        {
            public bool AllowSort { get; set; } = true;
            public bool AlwaysWild { get; set; } = false;
            public int CachedTableNo { get; set; } = 0;
            public int ContactType { get; set; }
            public int DataType { get; set; } = 1;
            public bool Descend { get; set; } = false;
            public string Descr { get; set; } = "Grup Firmasi No";
            public int Display { get; set; }
            public string Field { get; set; } = "CONTID";
            public int FieldNo { get; set; }
            public int Format { get; set; } = 1;
            public int Length { get; set; } = 32;
            public int ReqParam { get; set; } = 0;
            public string SearchGroup { get; set; } = "1";
            public int SortOrder { get; set; } = 0;
            public int Table { get; set; }
            public int UStatus { get; set; } = 0;
            public string Value { get; set; } = "*";
        }

        public sealed class ManitouSearchResponse
        {
            [JsonPropertyName("columns")]
            public List<string> Columns { get; set; } = new();

            [JsonPropertyName("maxRows")]
            public int MaxRows { get; set; }

            [JsonPropertyName("results")]
            public List<ManitouContactResult> Results { get; set; } = new();

            [JsonPropertyName("total")]
            public int Total { get; set; }
        }

        public sealed class ManitouContactResult
        {
            [JsonPropertyName("addr1")]
            public string? Addr1 { get; set; }

            [JsonPropertyName("cardNo")]
            public int? CardNo { get; set; }

            [JsonPropertyName("city")]
            public string? City { get; set; }

            [JsonPropertyName("commState")]
            public int? CommState { get; set; }

            [JsonPropertyName("contPoint")]
            public string? ContPoint { get; set; }

            [JsonPropertyName("country")]
            public int? Country { get; set; }

            [JsonPropertyName("dealerId")]
            public string? DealerId { get; set; }

            [JsonPropertyName("hidden")]
            public bool Hidden { get; set; }

            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("index")]
            public int? Index { get; set; }

            [JsonPropertyName("monitoringStatus")]
            public int? MonitoringStatus { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("region")]
            public string? Region { get; set; }

            [JsonPropertyName("serialNo")]
            public int SerialNo { get; set; }

            [JsonPropertyName("subType")]
            public int? SubType { get; set; }

            [JsonPropertyName("refContactPointHidden")]
            public bool? RefContactPointHidden { get; set; }

            [JsonPropertyName("refContactType")]
            public int? RefContactType { get; set; }

    }
}
