using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkFlowDtos.WorkFlowArchive
{
    public class WorkFlowArchiveFilterDto
    {
        public string? RequestNo { get; set; }
        public string? CustomerName { get; set; }       // JSON içinden filtrelenecek
        public string? TechnicianName { get; set; }     // JSON içinden filtrelenecek
        public string? ArchiveReason { get; set; }      // Completed / Cancelled vb.
        public DateTime? ArchivedFrom { get; set; }
        public DateTime? ArchivedTo { get; set; }

        // Pagination
        private int _page = 1;
        private int _pageSize = 20;

        public int Page
        {
            get => _page <= 0 ? 1 : _page;
            set => _page = value;
        }

        public int PageSize
        {
            get => _pageSize <= 0 ? 20 : (_pageSize > 100 ? 100 : _pageSize);
            set => _pageSize = value;
        }
    }
}
