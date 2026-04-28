using Aliyun.OSS;
using Aliyun.OSS.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PhotographyWorkflow.Api;
using PhotographyWorkflow.Api.Endpoints;
// 💡 引入你的项目命名空间，确保能找到相关的模型和分控室
using PhotographyWorkflow.Api.Models;
using System;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 🔌 第一阶段：服务注册 (接通后端核心电源)
// ==========================================

// 1. 注册数据库上下文 (SQLite 引擎)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=DawnDusk.db"));

// 2. 绑定 OSS 配置参数
builder.Services.Configure<OssOptions>(builder.Configuration.GetSection("OssConfig"));

// 注册阿里云 OSS 客户端（单例模式，整个程序共用一个"发电机"）
builder.Services.AddSingleton(sp =>
{
    //  填写Bucket所在地域对应的Endpoint。以华东1（杭州）为例，Endpoint填写为https://oss-cn-hangzhou.aliyuncs.com。
    var options = builder.Configuration.GetSection("OssConfig").Get<OssOptions>();
    var config = new ClientConfiguration()
    {
        ConnectionTimeout = 60000, // 连接超时时间，默认60秒
        MaxErrorRetry = 5, // 最大错误重试次数，默认3次
        IsCname = false, // 是否使用CNAME，默认false
    };

    // 设置v4签名。
    config.SignatureVersion = SignatureVersion.V4;
    // 创建OssClient实例。
    var ossClient = new OssClient(options.Endpoint, options.AccessKeyId, options.AccessKeySecret, config);

    // 填写Bucket所在地域对应的Region
    ossClient.SetRegion(options.Region);
    return ossClient;

});

// 4. 允许跨域 (局域网内不同设备通讯保障)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

// 5. 注册 Swagger 测试台服务
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

// ==========================================
// 🚰 第二阶段：请求管道配置 (布置数据流向)
// ==========================================

// 启用 Swagger 面板 (仅在开发环境下启用)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "朝夕(Dawn&Dusk) API 测试台 v1");
        c.DocumentTitle = "朝夕 - API 调试总控台";
    });
}

// 启用跨域中间件
app.UseCors("AllowAll");

// 开启前端网页静态服务 (指向 wwwroot 文件夹，你的 index.html 和 admin.html 都在这)
app.UseDefaultFiles();
app.UseStaticFiles();

// [备用预留] 开启本地照片直读管线 (把内部 Uploads 文件夹映射为 /photos 网址)
string localStoragePath = Path.Combine(app.Environment.ContentRootPath, "Uploads");
if (!Directory.Exists(localStoragePath))
{
    Directory.CreateDirectory(localStoragePath);
}
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(localStoragePath),
    RequestPath = "/photos"
});


// ==========================================
// 💡 第三阶段：路由挂载 (接通业务分控室)
// ==========================================

// 将具体的业务逻辑全部下放给端点扩展方法，保持主控室极致整洁
app.MapGalleryEndpoints();  // 接通客户端画廊大厅的电缆 (/api/photos/...)
app.MapAdminEndpoints();    // 接通摄影师发货后台的电缆 (/api/admin/...)


// ==========================================
// 🚀 第四阶段：启动系统
// ==========================================

// 监听所有网络接口，让手机和平板也能通过局域网 IP 访问
app.Urls.Add("http://0.0.0.0:5000");



// 在 app.Run() 之前的初始化逻辑里
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();

    // 💡 核心指令：这行代码会检查数据库是否存在，不存在则根据最新模型创建
    // 如果数据库存在但结构不对，它不会更新结构。
    // 但如果你删除了 .db 文件，它会重新生成一个完美的最新版。
    // context.Database.EnsureCreated();

    // 💡 新引擎：启动时自动检查图纸，缺什么字段就自动补什么字段，绝不伤害老数据！
    context.Database.Migrate();
}
// 正式合闸运转
app.Run();