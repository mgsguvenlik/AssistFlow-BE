using Model.Dtos.Manitou;
using System.Threading.Tasks;

namespace Business.Interfaces.Manitou
{
    public interface IManitouApiService
    {
        Task<TResponse?> SendAsync<TResponse>(
            HttpMethod method,
            string relativeUrl,
            object? requestBody = null,
            string? accessToken = null,
            CancellationToken cancellationToken = default);

        Task<string?> LoginAsync(
            CancellationToken cancellationToken = default);

        Task<List<ManitouContactResult>> GetCustomerGroupsAsync(
            string accessToken,
            CancellationToken cancellationToken = default);

        Task<List<ManitouContactResult>> GetCustomersByGroupCodeAsync(
            string accessToken,
            string groupCode,
            CancellationToken cancellationToken = default);

        // 1.1 Eski test sonuçlarını sıfırlar.
        Task BeginSystemTestAsync(
            string accessToken,
            int serialNo,
            CancellationToken cancellationToken = default);

        // 1.2 Hesabı On-Test statüsüne alır.
        // Çalışma başlat ve çalışma uzat aynı endpointi kullanır.
        Task SetCustomerOnTestAsync(
            string accessToken,
            ManitouOnTestRequest request,
            CancellationToken cancellationToken = default);

        // 2.1 Zone listesini ve test sinyal durumlarını getirir.
        Task<List<ManitouSystemTestZoneResult>> QuerySystemTestAsync(
            string accessToken,
            int serialNo,
            CancellationToken cancellationToken = default);

        // 2.2 Aktivite/Sinyal listesini getirir.
        Task<ManitouCustomerActivityResponse?> GetCustomerActivityAsync(
            string accessToken,
            int serialNo,
            int days = 1,
            CancellationToken cancellationToken = default);

        // 4.1 Öncesi aktif/uzatılmış çalışma kayıtlarını getirir.
        // Response örneği henüz olmadığı için raw string dönüyoruz.
        Task<List<ManitouOutOfServiceResult>> GetOutOfServiceAsync(
            string accessToken,
            int serialNo,
            CancellationToken cancellationToken = default);

        // 4.1 Hesabı test modundan çıkarır.
        Task SetCustomerOffTestAsync(
            string accessToken,
            ManitouOffTestRequest request,
            CancellationToken cancellationToken = default);
    }
}