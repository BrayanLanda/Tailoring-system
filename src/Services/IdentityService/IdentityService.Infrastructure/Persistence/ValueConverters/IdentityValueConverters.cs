using IdentityService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace IdentityService.Infrastructure.Persistence.ValueConverters;

internal sealed class TailorInvitationIdConverter()
    : ValueConverter<TailorInvitationId, Guid>(id => id.Value, value => new TailorInvitationId(value));

internal sealed class TailorProfileIdConverter()
    : ValueConverter<TailorProfileId, Guid>(id => id.Value, value => new TailorProfileId(value));

internal sealed class EmailAddressConverter()
    : ValueConverter<EmailAddress, string>(email => email.Value, value => EmailAddress.Of(value));

internal sealed class InvitationTokenHashConverter()
    : ValueConverter<InvitationTokenHash, string>(hash => hash.Value, value => InvitationTokenHash.Of(value));
