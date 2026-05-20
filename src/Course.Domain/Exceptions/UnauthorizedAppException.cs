using System.Net;

namespace Course.Domain.Exceptions;

public class UnauthorizedAppException : AppException
{
    public UnauthorizedAppException(string message)
        : base(message, HttpStatusCode.Unauthorized)
    {
    }
}
