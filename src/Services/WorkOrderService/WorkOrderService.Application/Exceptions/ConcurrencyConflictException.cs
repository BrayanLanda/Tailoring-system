namespace WorkOrderService.Application.Exceptions;

public sealed class ConcurrencyConflictException(Exception innerException) : Exception("The record was modified by someone else. Reload it and try again.", innerException);
