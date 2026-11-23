namespace IdentityService.Application.Exceptions;

public sealed class AuthenticationFailedException(string message) : Exception(message);
