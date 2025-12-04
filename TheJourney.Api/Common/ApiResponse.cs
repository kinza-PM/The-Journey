namespace TheJourney.Api.Common;

public class ApiResponse<T>
{
    public int Status { get; set; }
    public string Message { get; set; }
    public T? Data { get; set; }

    public ApiResponse(int status, string message, T? data = default)
    {
        Status = status;
        Message = message;
        Data = data;
    }

    public static ApiResponse<T> Success(T data, string message = "success")
    {
        return new ApiResponse<T>(200, message, data);
    }

    public static ApiResponse<T> Created(T data, string message = "created")
    {
        return new ApiResponse<T>(201, message, data);
    }

    public static ApiResponse<T> Error(int status, string message)
    {
        return new ApiResponse<T>(status, message, default);
    }

    public static ApiResponse<T> BadRequest(string message)
    {
        return new ApiResponse<T>(400, message, default);
    }

    public static ApiResponse<T> Unauthorized(string message)
    {
        return new ApiResponse<T>(401, message, default);
    }

    public static ApiResponse<T> NotFound(string message)
    {
        return new ApiResponse<T>(404, message, default);
    }

    public static ApiResponse<T> InternalError(string message)
    {
        return new ApiResponse<T>(500, message, default);
    }
}

