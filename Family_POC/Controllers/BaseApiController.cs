using Microsoft.AspNetCore.Mvc;

namespace Family_POC.Controllers
{
    [ApiController]
    public class BaseApiController : ControllerBase
    {
        protected OkObjectResult Success(string message = "success")
        {
            return Ok(new ResponseResult() { RtnCode = 0, Msg = message });
        }

        protected OkObjectResult Success<T>(T content, string message = "success")
        {
            return Ok(new ResponseResult<T>() { RtnCode = 0, Msg = message, Data = content });
        }

        protected ResponseResult<T> SuccessResult<T>(T content, string message = "success")
        {
            return new ResponseResult<T>() { RtnCode = 0, Msg = message, Data = content };
        }

        protected OkObjectResult Failure(string message = "an error occur, please try again, thanks")
        {
            return Ok(new ResponseResult() { RtnCode = 99, Msg = message });
        }

        protected OkObjectResult Failure(int returnCode, string message = "an error occur, please try again, thanks")
        {
            return Ok(new ResponseResult() { RtnCode = returnCode, Msg = message });
        }

        protected OkObjectResult Failure<T>(T content, string message = "an error occur, please try again, thanks")
        {
            return Ok(new ResponseResult<T>() { RtnCode = 99, Msg = message, Data = content });
        }
        protected ResponseResult<T> FaliureResult<T>(T content, string message = "an error occur, please try again, thanks")
        {
            return new ResponseResult<T>() { RtnCode = 99, Msg = message, Data = content };
        }
    }

    public class ResponseResult
    {
        public int RtnCode { get; set; }

        public string Msg { get; set; }
    }

    public class ResponseResult<T>
    {
        public int RtnCode { get; set; }

        public string Msg { get; set; }

        public T Data { get; set; }
    }
}
