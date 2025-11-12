using Business.Interfaces;
using Model.Dtos.Auth;

namespace Business.Services
{
    public sealed class CurrentUser : ICurrentUser
    {
        private readonly IAuthService _auth;
        private CurrentUserDto? _me;
        private bool _loaded;

        public CurrentUser(IAuthService auth) => _auth = auth;

        public async ValueTask<CurrentUserDto?> GetAsync(CancellationToken ct = default)
        {
            if (_loaded) return _me;
            _me = (await _auth.MeAsync())?.Data;
            _loaded = true;
            return _me;
        }

        public long Id => _me?.Id ?? 0;
        public string? Email => _me?.Email;
        public string? TechnicianName => _me?.TechnicianName;
    }

}
