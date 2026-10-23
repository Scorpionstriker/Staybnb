namespace Staybnb.Models.Common
{
    /// <summary>
    /// What a service method returns: did it work, and a message to show the user.
    /// Controllers typically do:  TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
    /// </summary>
    public class ServiceResult
    {
        public bool Succeeded { get; protected set; }
        public string Message { get; protected set; } = string.Empty;

        public static ServiceResult Ok(string message = "") => new() { Succeeded = true, Message = message };
        public static ServiceResult Fail(string message) => new() { Succeeded = false, Message = message };
    }

    /// <summary>Same as ServiceResult but also carries the created/updated object.</summary>
    public class ServiceResult<T> : ServiceResult
    {
        public T? Data { get; private set; }

        public static ServiceResult<T> Ok(T data, string message = "") =>
            new() { Succeeded = true, Message = message, Data = data };

        public static new ServiceResult<T> Fail(string message) =>
            new() { Succeeded = false, Message = message };
    }
}
