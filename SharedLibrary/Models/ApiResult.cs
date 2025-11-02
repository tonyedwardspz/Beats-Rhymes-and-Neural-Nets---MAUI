namespace SharedLibrary.Models;

/// <summary>
/// Result wrapper for API operations with standardized success/error handling
/// </summary>
/// <typeparam name="T">The type of the result data</typeparam>
public class ApiResult<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public int StatusCode { get; set; }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    /// <param name="data">The data to return</param>
    /// <param name="statusCode">HTTP status code (default: 200)</param>
    /// <returns>Successful ApiResult instance</returns>
    public static ApiResult<T> Success(T data, int statusCode = 200)
    {
        return new ApiResult<T>
        {
            IsSuccess = true,
            Data = data,
            StatusCode = statusCode
        };
    }

    /// <summary>
    /// Creates a failed result
    /// </summary>
    /// <param name="errorMessage">Error message describing the failure</param>
    /// <param name="statusCode">HTTP status code (default: 500)</param>
    /// <returns>Failed ApiResult instance</returns>
    public static ApiResult<T> Failure(string errorMessage, int statusCode = 500)
    {
        return new ApiResult<T>
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            StatusCode = statusCode
        };
    }
}

