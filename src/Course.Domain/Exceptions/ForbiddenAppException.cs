using System.Net;

namespace Course.Domain.Exceptions;

public class ForbiddenAppException : AppException
{
    public ForbiddenAppException(string message)
        : base(message, HttpStatusCode.Forbidden)
    {
    }
}
