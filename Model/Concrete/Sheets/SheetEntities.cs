using Model.Abstractions;

namespace Model.Concrete.Sheets;

public sealed class SheetWorkbook : BaseEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public long OwnerId { get; set; }
    public Guid ActiveRevisionId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class SheetRevision : BaseEntity
{
    public Guid Id { get; set; }
    public Guid WorkbookId { get; set; }
    public Guid? BaseRevisionId { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string SnapshotJson { get; set; } = "{}";
    // Imported revisions are private drafts until an explicit save publishes them.
    public bool IsDraft { get; set; }
}

public sealed class SheetDataBlock : BaseEntity
{
    public Guid Id { get; set; }
    public Guid WorkbookId { get; set; }
    public string CellsJson { get; set; } = "[]";
}

public sealed class SheetAccess : BaseEntity
{
    public Guid WorkbookId { get; set; }
    public long UserId { get; set; }
    public long GrantedBy { get; set; }
    public DateTime GrantedAtUtc { get; set; }
}

public sealed class SheetActivity : BaseEntity
{
    public long Id { get; set; }
    public Guid WorkbookId { get; set; }
    public long UserId { get; set; }
    public Guid? RevisionId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string Action { get; set; } = "";
    public string DetailJson { get; set; } = "{}";
}

