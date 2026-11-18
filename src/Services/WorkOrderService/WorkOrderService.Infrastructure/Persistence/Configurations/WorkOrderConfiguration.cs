using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkOrderService.Domain.Entities;

namespace WorkOrderService.Infrastructure.Persistence.Configurations;

internal sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("work_orders");

        builder.Ignore(w => w.DomainEvents);
        builder.Ignore(w => w.AmountPaid);
        builder.Ignore(w => w.Balance);
        builder.Ignore(w => w.IsLinkedToOriginal);

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id)
            .HasConversion<WorkOrderIdConverter>()
            .ValueGeneratedNever();

        builder.Property(w => w.SerialNumber).HasConversion<SerialNumberConverter>().IsRequired();
        builder.HasIndex(w => w.SerialNumber).IsUnique();

        builder.Property(w => w.CustomerName).IsRequired();
        builder.Property(w => w.CustomerPhone)
            .HasConversion<PhoneNumberConverter>()
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(w => w.GarmentDescription).IsRequired();

        builder.Property(w => w.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(w => w.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(w => w.PaymentStatus).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.Property(w => w.TailorId).HasConversion<TailorIdConverter>();
        builder.Property(w => w.OriginalTailorId).HasConversion<TailorIdConverter>();
        builder.Property(w => w.OriginalWorkOrderId).HasConversion<WorkOrderIdConverter>();

        builder.Property(w => w.Price).HasConversion<MoneyConverter>().HasPrecision(18, 2);

        builder.OwnsMany(w => w.Payments, payments =>
        {
            payments.ToTable("payments");
            payments.WithOwner().HasForeignKey("WorkOrderId");
            payments.HasKey(p => p.Id);
            payments.Property(p => p.Id)
                .HasConversion<PaymentIdConverter>()
                .ValueGeneratedNever();
            payments.Property(p => p.Amount)
                .HasConversion<MoneyConverter>()
                .HasPrecision(18, 2)
                .IsRequired();
            payments.Property(p => p.PaidAt).IsRequired();
        });
        builder.Navigation(w => w.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(w => w.Jobs, jobs =>
        {
            jobs.ToTable("work_order_jobs");
            jobs.WithOwner().HasForeignKey("WorkOrderId");
            jobs.Property<int>("Id");
            jobs.HasKey("Id");
            jobs.Property(j => j.Description).IsRequired();
            jobs.Property(j => j.Quantity).IsRequired();
        });
        builder.Navigation(w => w.Jobs).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsOne(w => w.Compensation, compensation =>
        {
            compensation.Property(c => c.Amount)
                .HasConversion<MoneyConverter>()
                .HasPrecision(18, 2)
                .IsRequired();
            compensation.Property(c => c.AuthorizedBy).IsRequired();
            compensation.Property(c => c.AuthorizedAt).IsRequired();
        });
    }
}
