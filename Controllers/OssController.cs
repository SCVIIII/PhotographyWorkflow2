using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System;


namespace PhotographyWorkflow.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OssController : ControllerBase
    {
        // 💡 声明一个只读的 IConfiguration 接口
        private readonly IConfiguration _configuration;

        // 💡 构造函数注入：ASP.NET 引擎启动时，会自动把系统配置塞进这个参数里
        public OssController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("signature")]
        public IActionResult GetSignature([FromQuery] string albumId)
        {
            if (string.IsNullOrEmpty(albumId)) return BadRequest("缺少相册ID");

            // 💡 像读取字典一样，用 "节点:属性" 的语法从 appsettings.json 中实时读取密钥
            string accessKeyId = _configuration["AliyunOss:AccessKeyId"];
            string accessKeySecret = _configuration["AliyunOss:AccessKeySecret"];
            string host = _configuration["AliyunOss:Host"];

            // 1. 设置通行证有效期 (比如 300 秒)
            var expireTime = DateTime.UtcNow.AddSeconds(300);
            string expiration = expireTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");

            // 2. 规定照片只能上传到指定的相册文件夹下
            string dir = $"albums/{albumId}/";

            // 3. 构建 Policy 策略 (JSON格式)
            var policyDict = new
            {
                expiration = expiration,
                conditions = new object[]
                {
                new object[] { "starts-with", "$key", dir },
                new object[] { "content-length-range", 0, 10485760 }
                }
            };

            string policyJson = JsonSerializer.Serialize(policyDict);

            // 4. 将 Policy 进行 Base64 编码
            string policyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(policyJson));

            // 5. 使用 AccessKeySecret 对 Policy 进行 HMAC-SHA1 签名
            string signature = ComputeSignature(accessKeySecret, policyBase64);

            // 6. 完美组装，返回给小程序前端
            return Ok(new
            {
                host = host,
                dir = dir,
                policy = policyBase64,
                signature = signature,
                accessId = accessKeyId,
                expire = ((DateTimeOffset)expireTime).ToUnixTimeSeconds()
            });
        }

        // 辅助加密方法保持不变
        private string ComputeSignature(string key, string data)
        {
            using (var algorithm = new HMACSHA1(Encoding.UTF8.GetBytes(key)))
            {
                byte[] hashBytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(data));
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}
