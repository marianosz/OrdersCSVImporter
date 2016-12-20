using System.Net;

namespace OrdersCSVImporter.API
{
    public class APIRequestResult
    {
        public bool HasError { get; set; }

        public string ErrorMessage { get; set; }

        public HttpStatusCode StatusCode { get; set; }
    }

    public class APIRequestResult<T> : APIRequestResult
    {
        public T Data { get; set; }
    }
}
