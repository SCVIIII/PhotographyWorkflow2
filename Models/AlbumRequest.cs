namespace PhotographyWorkflow.Api.Models
{
    // 💡 一个极其轻量的只读数据包，专门用来接收前端 admin.html 发来的表单数据
    public record AlbumRequest(string? Name, string? PhoneNumber, int ExpiryDays);
}
