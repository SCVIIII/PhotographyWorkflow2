


using System;

namespace PhotographyWorkflow.Api
{
    public class PhotoSelection
    {
        public int Id { get; set; }               // 流水号 (主键)
        public string AlbumId { get; set; }       // 相册编号 (例如 Wang_0520)

        // 💡 修正 1：允许为空的手机号
        // 在 string 后面加个问号 '?'，在 C# 中明确表示这个字段可以为 null
        public string? PhoneNumber { get; set; }

        public string SelectedPhotos { get; set; } // 选中的照片列表

        // 💡 修正 2：区分存入时间和保留天数
        public DateTime SubmitTime { get; set; }  // 客户提交的时间 (存入时间)
        public int RetentionDays { get; set; }    // 保留天数 (默认可以设为 7)

        public bool IsProcessed { get; set; }     // 电脑端是否已处理

        // 💡 架构师彩蛋：只读的计算属性 (辅助字段，不会真正存入数据库)
        // 这样你以后在写“自动清理代码”时，直接问它“过期了吗？”就可以了
        public bool IsExpired
        {
            get
            {
                // 💡 修正 3：9999 永久保留逻辑
                if (RetentionDays == 9999) return false; // 永远不过期

                // 当前时间 > 提交时间 + 保留天数，说明过期了
                return DateTime.Now > SubmitTime.AddDays(RetentionDays);
            }
        }
    }
}