using Aliyun.OSS;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PhotographyWorkflow.Api.Models;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace PhotographyWorkflow.Api.Endpoints
{
    public static class GalleryEndpoints
    {
        public static void MapGalleryEndpoints(this WebApplication app)
        {

            // 路由分组，以下接口自动加上 /api/photos 前缀
            var group = app.MapGroup("/api/photos");

            // 1. 获取相册照片列表 (供 index.html 和 select.html 共同调用)
            // 💡 修复3：新增 mode 参数，让前端亮明身份
            // 1. 获取相册照片列表 (供 index.html 和 select.html 共同调用)
            group.MapGet("/list", async ([FromQuery] string album, [FromQuery] string? mode, AppDbContext db, IOptions<OssOptions> options, OssClient ossClient) =>
            {
                var albumRecord = await db.Albums.FindAsync(album);
                if (albumRecord == null || albumRecord.Status == 2)
                {
                    return Results.NotFound(new { message = "时光胶囊已封存，该相册不存在或已被彻底擦除" });
                }

                var config = options.Value;
                var allPhotos = new List<string>();
                try
                {
                    var listRequest = new ListObjectsRequest(config.BucketName) { Prefix = $"{album}/" };
                    var listResult = ossClient.ListObjects(listRequest);

                    foreach (var obj in listResult.ObjectSummaries)
                    {
                        var key = obj.Key;
                        if (!key.EndsWith("/"))
                        {
                            allPhotos.Add(key.Split('/').Last());
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[OSS读取失败]: {ex.Message}");
                    return Results.Problem("服务器连接云端相册失败");
                }

                // 💡 终极修复版：严格遵守语法，且区分业务场景的 URL 生成器
                // 💡 改进版：新增 watermarkSize 参数，动态控制水印大小
                string GenerateSafeUrl(string key, int hours, string baseProcess = null, int watermarkSize = 0)
                {
                    var request = new GeneratePresignedUriRequest(config.BucketName, key, SignHttpMethod.Get)
                    {
                        Expiration = DateTime.Now.AddHours(hours)
                    };

                    // 💡 只有选片模式且设定了字号时，才生成水印指令
                    // 💡 抛弃有缺陷的 fill_1，采用“三点矩阵”全方位压印
                    // t_25 保证了足够的显影浓度，同时又不会完全糊死人脸
                    string watermarkAction = (mode == "select" && watermarkSize > 0)
                        ? $"watermark,text_dGFuZ2lzbGUudG9w,color_FFFFFF,size_{watermarkSize},g_nw,x_50,y_50,t_75" +
                          $"/watermark,text_dGFuZ2lzbGUudG9w,color_FFFFFF,size_{watermarkSize},g_center,t_55" +
                          $"/watermark,text_dGFuZ2lzbGUudG9w,color_FFFFFF,size_{watermarkSize},g_se,x_50,y_50,t_75"
                        : null;

                    if (!string.IsNullOrEmpty(baseProcess) && !string.IsNullOrEmpty(watermarkAction))
                    {
                        request.Process = $"{baseProcess}/{watermarkAction}";
                    }
                    else if (!string.IsNullOrEmpty(baseProcess))
                    {
                        request.Process = baseProcess;
                    }
                    else if (!string.IsNullOrEmpty(watermarkAction))
                    {
                        request.Process = $"image/{watermarkAction}";
                    }

                    var rawUrl = ossClient.GeneratePresignedUri(request).ToString();

                    if (rawUrl.StartsWith("http://")) rawUrl = rawUrl.Replace("http://", "https://");
                    if (rawUrl.Contains("-internal.aliyuncs")) rawUrl = rawUrl.Replace("-internal.aliyuncs", ".aliyuncs");
                    return rawUrl;
                }

                bool hidePhotos = (mode == "select" && !albumRecord.CanSelect);

                var photoList = !hidePhotos
                    ? allPhotos.Select(p => new
                    {
                        key = p,
                        // 1. 缩略图 (约 500px)：使用较小的 20 号字
                        thumbUrl = GenerateSafeUrl($"{albumRecord.Id}/{p}", 2, "image/resize,m_fill,w_500,h_500/quality,q_80", 20),

                        // 2. 预览图 (约 1920px)：使用适中的 60 号字
                        displayUrl = GenerateSafeUrl($"{albumRecord.Id}/{p}", 2, "image/resize,l_1920/quality,q_85", 60),

                        // 3. 原图下载 (约 6000px)：使用超大的 200 号字！
                        downloadUrl = GenerateSafeUrl($"{albumRecord.Id}/{p}", 24, null, 200)
                    }).ToList<object>()
                    : new List<object>();

                return Results.Ok(new
                {
                    id = albumRecord.Id,
                    name = albumRecord.Name,
                    canDownload = albumRecord.CanDownload,
                    canSelect = albumRecord.CanSelect,
                    photos = photoList
                });
            });

            // 2. 接收并保存客户的选片清单
            group.MapPost("/submit-selection", async (PhotoSelection payload, AppDbContext db) =>
            {
                // 💡 新增防火墙校验：检查相册实体是否存在，以及选片通道是否开启
                var album = await db.Albums.FindAsync(payload.AlbumId);
                if (album == null || !album.CanSelect)
                {
                    // 拒绝一切在封板后的非法提交请求
                    return Results.BadRequest("选片通道已关闭或相册不存在");
                }

                // 自动打上当前的系统时间
                payload.SubmitTime = DateTime.Now;
                // 默认状态为未处理（摄影师还没看）
                payload.IsProcessed = false;

                db.Selections.Add(payload);
                await db.SaveChangesAsync();

                return Results.Ok(new { message = "选片清单已成功保存" });
            });


            // 3. 一键打包下载
            group.MapGet("/download-all", async (string album, IOptions<OssOptions> options, OssClient ossClient, HttpContext context, AppDbContext db) =>
            {
                var albumRecord = await db.Albums.FindAsync(album);

                // 💡 物理拦截：如果未找到相册，或者开关没打开，直接拒绝！
                if (albumRecord == null || !albumRecord.CanDownload)
                {
                    context.Response.StatusCode = 403;
                    context.Response.ContentType = "text/plain; charset=utf-8";
                    await context.Response.WriteAsync("未获授权，请联系摄影师开通下载权限");
                    return;
                }

                var syncIoFeature = context.Features.Get<IHttpBodyControlFeature>();
                if (syncIoFeature != null) syncIoFeature.AllowSynchronousIO = true;

                var config = options.Value;
                var listRequest = new ListObjectsRequest(config.BucketName) { Prefix = $"{album}/" };
                var result = ossClient.ListObjects(listRequest);
                var photos = result.ObjectSummaries.Where(obj => !obj.Key.EndsWith("/")).ToList();

                if (!photos.Any())
                {
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsync("相册为空或已过期");
                    return;
                }

                long totalSizeBytes = photos.Sum(p => p.Size);
                albumRecord.DownloadCount += 1;
                albumRecord.TotalBytesDownloaded += totalSizeBytes;
                await db.SaveChangesAsync();

                context.Response.ContentType = "application/zip";
                string zipFileName = $"{album}_棠屿朝夕_精修交付.zip";
                context.Response.Headers.Append("Content-Disposition", $"attachment; filename*=UTF-8''{Uri.EscapeDataString(zipFileName)}");

                using (var zipArchive = new ZipArchive(context.Response.Body, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var photo in photos)
                    {
                        var entryName = Path.GetFileName(photo.Key);
                        var entry = zipArchive.CreateEntry(entryName, CompressionLevel.Fastest);
                        using (var entryStream = entry.Open())
                        {
                            var ossObject = ossClient.GetObject(config.BucketName, photo.Key);
                            using (var ossStream = ossObject.Content)
                            {
                                await ossStream.CopyToAsync(entryStream);
                            }
                        }
                    }
                }
            });

        }
    }
}