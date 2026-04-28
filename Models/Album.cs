using System;

namespace PhotographyWorkflow.Api.Models
{
    public class Album
    {
        // 核心标识
        public string Id { get; set; } = ""; // 8位短码
        public string Name { get; set; } = ""; // 相册名

        // 客户与备注 (扩容部分)
        public string? ClientName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Notes { get; set; }

        // 业务状态 (关键列)
        public bool IsPaid { get; set; } = false;
        public bool CanDownload { get; set; } = false; // 💡 新增：客户是否可以下载原图

        // 1: 在线 (Online), 2: 已擦除 (Privacy Wiped)
        public int Status { get; set; } = 1;

        // 时间管理
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ExpiryDate { get; set; }

        // 👇 新增的流量计字段
        public int DownloadCount { get; set; } = 0;           // 客户打包下载的次数
        public long TotalBytesDownloaded { get; set; } = 0;   // 累计消耗的流量 (字节)
    }
}