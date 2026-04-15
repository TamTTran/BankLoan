using LoanFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoanFlow.Infrastructure.Data.Configurations;

public class OverdueDailySnapshotConfiguration : IEntityTypeConfiguration<OverdueDailySnapshot>
{
    public void Configure(EntityTypeBuilder<OverdueDailySnapshot> builder)
    {
        builder.HasKey(s => s.Id);

        // Prevent duplicate snapshots: one record per loan per day
        builder.HasIndex(s => new { s.LoanId, s.SnapshotDate })
            .IsUnique();

        // Efficient date-range queries for reporting
        builder.HasIndex(s => s.SnapshotDate);

        builder.Property(s => s.Bucket).HasMaxLength(20).IsRequired();
        builder.Property(s => s.OverdueAmount).HasPrecision(18, 2);
        builder.Property(s => s.OutstandingAmount).HasPrecision(18, 2);
        builder.Property(s => s.LateFeeAccrued).HasPrecision(18, 2);

        builder.HasOne(s => s.Loan)
            .WithMany()
            .HasForeignKey(s => s.LoanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
