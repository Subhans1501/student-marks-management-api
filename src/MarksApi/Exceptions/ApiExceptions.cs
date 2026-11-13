namespace MarksApi.Exceptions;

public abstract class ApiException : Exception
{
    public int StatusCode { get; }
    public string Title { get; }

    protected ApiException(int statusCode, string title, string detail)
        : base(detail)
    {
        StatusCode = statusCode;
        Title = title;
    }
}

public sealed class NotFoundException : ApiException
{
    public NotFoundException(string title, string detail)
        : base(StatusCodes.Status404NotFound, title, detail)
    {
    }
}

public sealed class ConflictException : ApiException
{
    public ConflictException(string title, string detail)
        : base(StatusCodes.Status409Conflict, title, detail)
    {
    }
}

public sealed class ForbiddenException : ApiException
{
    public ForbiddenException(string title, string detail)
        : base(StatusCodes.Status403Forbidden, title, detail)
    {
    }
}

public sealed class UnauthorizedApiException : ApiException
{
    public UnauthorizedApiException(string title, string detail)
        : base(StatusCodes.Status401Unauthorized, title, detail)
    {
    }
}

public sealed class BadRequestException : ApiException
{
    public BadRequestException(string title, string detail)
        : base(StatusCodes.Status400BadRequest, title, detail)
    {
    }
}

