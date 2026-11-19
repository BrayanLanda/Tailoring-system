using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TailorService.Domain.Entities;

namespace TailorService.Infrastructure.Persistence.Configurations;

internal sealed class TailorConfiguration : IEntityTypeConfiguration<Tailor>
{
    public void Configure(EntityTypeBuilder<Tailor> builder)
    {
        builder.ToTable("tailors");
        builder.Ignore(tailor => tailor.DomainEvents);
        builder.Property<int>("Version").IsConcurrencyToken();

        builder.HasKey(tailor => tailor.Id);
        builder.Property(tailor => tailor.Id)
            .HasConversion<TailorIdConverter>()
            .ValueGeneratedNever();

        builder.Property(tailor => tailor.Name).IsRequired();
        builder.Property(tailor => tailor.IsActive).IsRequired();
    }
}
