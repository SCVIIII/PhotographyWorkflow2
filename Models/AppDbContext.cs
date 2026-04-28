using Microsoft.EntityFrameworkCore;
// 💡 引入根命名空间，以找到 PhotoSelection 类
using PhotographyWorkflow.Api;

namespace PhotographyWorkflow.Api.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // 💡 施工点：将相册表挂载到数据库上下文中
        // 这样你才能通过 db.Albums 来进行增删改查
        public DbSet<Album> Albums { get; set; }

        // 如果未来有“客户选片”记录表，也在这里添加
        // public DbSet<SelectionRecord> Selections { get; set; }

        // 💡 唤醒：将客户选片记录表重新挂载到数据库中
        public DbSet<PhotoSelection> Selections { get; set; }
    }
}
