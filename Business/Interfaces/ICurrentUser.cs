using Model.Dtos.Auth;

namespace Business.Interfaces
{
    public interface ICurrentUser
    {
        ValueTask<CurrentUserDto?> GetAsync(CancellationToken ct = default);
        long Id { get; }
        string? Email { get; }
        string? TechnicianName { get; }
    }

}
