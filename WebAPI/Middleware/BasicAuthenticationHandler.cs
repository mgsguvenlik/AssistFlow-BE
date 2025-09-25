using Azure.Core;
using Business.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace WebAPI.Middleware
{
    public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly IUserService _userService;

        public BasicAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            IUserService userService
        ) : base(options, logger, encoder, clock)
        {
            _userService = userService;
        }
        ///Ex Basic :  Basic YWRtaW46YWRtaW4=
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return AuthenticateResult.Fail("Missing Authorization Header");

            try
            {
                var authHeader = AuthenticationHeaderValue.Parse(Request.Headers["Authorization"]);
                var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader.Parameter ?? "")).Split(':');
                if (credentials.Length != 2)
                    return AuthenticateResult.Fail("Invalid Authorization Header");

                var userName = credentials[0];
                var password = credentials[1];

                // Kullanıcı doğrulama (IUserService ile) ///MZK Geri açılacak
                //var userResponse = await _userService.BasicAuthAsync(userName, password);
                //if (userResponse.IsSuccess && userResponse.Data != null)
                //{
                //    var claims = new[] {
                //            new Claim(ClaimTypes.NameIdentifier, userResponse.Data.Id.ToString()),
                //            new Claim(ClaimTypes.Name, userResponse.Data.UserName)
                //        };
                //    var identity = new ClaimsIdentity(claims, Scheme.Name);
                //    var principal = new ClaimsPrincipal(identity);
                //    var ticket = new AuthenticationTicket(principal, Scheme.Name);
                //    return AuthenticateResult.Success(ticket);
                //}
                //else
                //{
                //    return AuthenticateResult.Fail("Invalid User or Password");
                //}
                return null;
            }
            catch
            {
                return AuthenticateResult.Fail("Invalid Authorization Header");
            }
        }
    }
}
