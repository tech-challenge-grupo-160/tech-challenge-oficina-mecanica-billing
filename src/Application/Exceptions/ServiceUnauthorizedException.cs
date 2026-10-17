using System.Net;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Application.Exceptions;

public sealed class ServiceUnauthorizedException : UnauthorizedAccessException, IServiceExceptionContract
{
    public ServiceUnauthorizedException(string message) : base(message) { }
    public int StatusCode => (int)HttpStatusCode.Unauthorized;
}
