using System.Net;

namespace Course.Domain.Exceptions;

public class NotFoundAppException : AppException
{
    public NotFoundAppException(string message)
        : base(message, HttpStatusCode.NotFound)
    {
    }
}
