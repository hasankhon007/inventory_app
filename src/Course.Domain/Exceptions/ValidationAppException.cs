using System.Net;

namespace Course.Domain.Exceptions;

public class ValidationAppException : AppException
{
    public ValidationAppException(string message)
        : base(message, HttpStatusCode.BadRequest)
    {
    }
}
