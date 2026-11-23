using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkOrderService.Infrastructure.Persistence.Entities;

namespace WorkOrderService.Infrastructure.Persistence.Configurations;

internal sealed class AssignableTailorProjectionConfiguration : IEntityTypeConfiguration<AssignableTailorProjection>
{
    public void Configure(EntityTypeBuilder<AssignableTailorProjection> builder)
    {
        builder.ToTable("assignable_tailors");
        builder.HasKey(tailor => tailor.TailorId);
        builder.Property(tailor => tailor.TailorId).HasColumnName("tailor_id").ValueGeneratedNever();
        builder.Property(tailor => tailor.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(tailor => tailor.LastOccurredOn).HasColumnName("last_occurred_on").IsRequired();
    }
}
