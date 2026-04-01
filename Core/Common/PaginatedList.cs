namespace Core.Common
{
    /// <summary>
    /// Sayfalama için kullanılan generic liste sınıfı
    /// </summary>
    /// <typeparam name="T">Liste item tipi</typeparam>
    public class PaginatedList<T>
    {
        /// <summary>
        /// Mevcut sayfadaki öğeler
        /// </summary>
        public List<T> Items { get; set; }

        /// <summary>
        /// Toplam öğe sayısı
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Mevcut sayfa numarası
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// Sayfa başına öğe sayısı
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Toplam sayfa sayısı
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// Önceki sayfa var mı?
        /// </summary>
        public bool HasPreviousPage => PageNumber > 1;

        /// <summary>
        /// Sonraki sayfa var mı?
        /// </summary>
        public bool HasNextPage => PageNumber < TotalPages;

        public PaginatedList(List<T> items, int totalCount, int pageNumber, int pageSize)
        {
            Items = items;
            TotalCount = totalCount;
            PageNumber = pageNumber;
            PageSize = pageSize;
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        }

        /// <summary>
        /// Boş sayfalı liste oluşturur
        /// </summary>
        public static PaginatedList<T> Empty()
        {
            return new PaginatedList<T>(new List<T>(), 0, 1, 10);
        }
    }
}