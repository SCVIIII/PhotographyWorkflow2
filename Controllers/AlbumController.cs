using Microsoft.AspNetCore.Mvc;

namespace PhotographyWorkflow.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AlbumController : ControllerBase
    {
        // 假设你通过依赖注入引入了数据库上下文
        // private readonly AppDbContext _context;
        // public AlbumController(AppDbContext context) { _context = context; }

        [HttpPost("create")]
        public IActionResult CreateAlbum([FromBody] CreateAlbumRequest request)
        {
            // 1. 基础参数校验
            if (string.IsNullOrEmpty(request.AlbumId) || string.IsNullOrEmpty(request.CustomerName))
            {
                return BadRequest("相册ID和客户姓名不能为空");
            }

            try
            {
                // 2. 这里写你的数据库插入逻辑
                /*
                var newAlbum = new AlbumEntity 
                {
                    AlbumId = request.AlbumId,
                    CustomerName = request.CustomerName,
                    ShootDate = request.ShootDate,
                    RetentionDays = request.RetentionDays,
                    CreateTime = DateTime.Now
                };
                _context.Albums.Add(newAlbum);
                _context.SaveChanges();
                */

                // 模拟数据库操作成功
                Console.WriteLine($"成功创建相册: {request.AlbumId}, 客户: {request.CustomerName}");

                return Ok(new { success = true, message = "档案创建成功" });
            }
            catch (Exception ex)
            {
                // 记录日志...
                return StatusCode(500, "服务器内部错误，创建失败");
            }
        }
    }

    // 💡 定义接收数据的实体类 (DTO)
    public class CreateAlbumRequest
    {
        public string AlbumId { get; set; }
        public string CustomerName { get; set; }
        public string ShootDate { get; set; }
        public int RetentionDays { get; set; }
    }
}
