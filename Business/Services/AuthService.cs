using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Mapster;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete;
using Model.Dtos.Auth;
using System.Security.Claims;
using System.Text.Json;

public class AuthService : IAuthService
{
    private readonly IHttpContextAccessor _http;
    private readonly TypeAdapterConfig _config;

    public AuthService(IUnitOfWork uow,
                       IPasswordHasher<User> hasher,
                       IHttpContextAccessor http,
                       TypeAdapterConfig config)
    {
        _http = http;
        _config = config;
    }
    public Task<ResponseModel<CurrentUserDto>> MeAsync()
    {
        var user = _http.HttpContext?.User;
        var sess = _http.HttpContext?.Session;
        var dto = new CurrentUserDto { IsAuthenticated = user?.Identity?.IsAuthenticated ?? false };

        if (dto.IsAuthenticated && sess is not null)
        {
            dto.Id = long.TryParse(sess.GetString("Id"), out var id) ? id : 0;
            dto.Name = sess.GetString("Name") ?? "";
            dto.Email = sess.GetString("Email");
            var rolesJson = sess.GetString("Roles");
            dto.Roles = string.IsNullOrWhiteSpace(rolesJson)
                ? new List<string>()
                : (JsonSerializer.Deserialize<List<string>>(rolesJson) ?? new List<string>());
        }

        return Task.FromResult(ResponseModel<CurrentUserDto>.Success(dto));
    }
}
