using Microsoft.EntityFrameworkCore;
using Model.Concrete.Sheets;
using Model.Concrete;

namespace Data.Concrete.EfCore.Configurations;

public static class SheetsConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        var books = model.Entity<SheetWorkbook>();
        books.ToTable("Workbooks", "sheets");
        books.HasKey(x => x.Id);
        books.Property(x => x.Name).HasMaxLength(200).IsRequired();
        books.HasIndex(x => new { x.OwnerId, x.UpdatedAtUtc });
        books.HasIndex(x => x.UpdatedAtUtc);
        books.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);

        var revisions = model.Entity<SheetRevision>();
        revisions.ToTable("Revisions", "sheets");
        revisions.HasKey(x => x.Id);
        revisions.Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)");
        revisions.HasIndex(x => new { x.WorkbookId, x.CreatedAtUtc });
        revisions.HasOne<SheetWorkbook>().WithMany().HasForeignKey(x => x.WorkbookId).OnDelete(DeleteBehavior.Cascade);

        var blocks = model.Entity<SheetDataBlock>();
        blocks.ToTable("DataBlocks", "sheets");
        blocks.HasKey(x => x.Id);
        blocks.Property(x => x.CellsJson).HasColumnType("nvarchar(max)");
        blocks.HasIndex(x => x.WorkbookId);
        blocks.HasOne<SheetWorkbook>().WithMany().HasForeignKey(x => x.WorkbookId).OnDelete(DeleteBehavior.Cascade);

        var access = model.Entity<SheetAccess>();
        access.ToTable("Access", "sheets");
        access.HasKey(x => new { x.WorkbookId, x.UserId });
        access.HasIndex(x => new { x.UserId, x.WorkbookId });
        access.HasOne<SheetWorkbook>().WithMany().HasForeignKey(x => x.WorkbookId).OnDelete(DeleteBehavior.Cascade);
        access.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        var activity = model.Entity<SheetActivity>();
        activity.ToTable("Activities", "sheets");
        activity.HasKey(x => x.Id);
        activity.Property(x => x.Action).HasMaxLength(40);
        activity.Property(x => x.DetailJson).HasColumnType("nvarchar(max)");
        activity.HasIndex(x => new { x.WorkbookId, x.OccurredAtUtc });
        activity.HasOne<SheetWorkbook>().WithMany().HasForeignKey(x => x.WorkbookId).OnDelete(DeleteBehavior.Cascade);
    }
}

