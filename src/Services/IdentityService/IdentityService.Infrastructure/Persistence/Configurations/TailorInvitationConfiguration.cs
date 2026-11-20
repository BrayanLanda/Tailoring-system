using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Persistence.ValueConverters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityService.Infrastructure.Persistence.Configurations;

internal sealed class TailorInvitationConfiguration : IEntityTypeConfiguration<TailorInvitation>
{
    public void Configure(EntityTypeBuilder<TailorInvitation> builder)
    {
        builder.ToTable("tailor_invitations");
        builder.Ignore(invitation => invitation.DomainEvents);
        builder.Property<int>("Version").IsConcurrencyToken();

        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.Id)
            .HasConversion<TailorInvitationIdConverter>()
            .ValueGeneratedNever();

        builder.Property(invitation => invitation.Email)
            .HasConversion<EmailAddressConverter>()
            .IsRequired();
        builder.HasIndex(invitation => invitation.Email).IsUnique();

        builder.Property(invitation => invitation.TailorProfileId)
            .HasConversion<TailorProfileIdConverter>()
            .IsRequired();
        builder.HasIndex(invitation => invitation.TailorProfileId).IsUnique();

        builder.Property(invitation => invitation.TokenHash)
            .HasConversion<InvitationTokenHashConverter>()
            .IsRequired();
        builder.Property(invitation => invitation.IssuedAt).IsRequired();
        builder.Property(invitation => invitation.ExpiresAt).IsRequired();
        builder.Property(invitation => invitation.AcceptedAt);
    }
}
