namespace IdentityService.Application.Exceptions;

public sealed class ConcurrencyConflictException(Exception innerException)
    : Exception("The invitation was modified by someone else. Reload it and try again.", innerException);
