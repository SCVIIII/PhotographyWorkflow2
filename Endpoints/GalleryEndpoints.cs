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
            group.MapGet("/list", async ([FromQuery] string album, AppDbContext db, IOptions<OssOptions> options, OssClient ossClient) =>
            {
                // 1.1 检查相册是否存在以及状态是否合法
                var albumRecord = await db.Albums.FindAsync(album);
                if (albumRecord == null || albumRecord.Status == 2)
                {
                    return Results.NotFound(new { message = "时光胶囊已封存，该相册不存在或已被彻底擦除" });
                }

                // 1.2 从 OSS 拉取该相册目录下的物理照片列表
                var config = options.Value;
                var allPhotos = new List<string>();
                try
                {
                    var listRequest = new ListObjectsRequest(config.BucketName) { Prefix = $"{album}/" };
                    var listResult = ossClient.ListObjects(listRequest);

                    foreach (var obj in listResult.ObjectSummaries)
                    {
                        var key = obj.Key;
                        // 排除目录本身和 thumbs 文件夹里的缩略图，只抓取原图文件名
                        if (!key.EndsWith("/") && !key.Contains("/thumbs/"))
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

                // 1.3 💡 物理隔离隐私保护：判断是否允许选片
                var photoList = albumRecord.CanSelect
                    ? allPhotos.Select(p => new
                    {
                        key = p,
                        // 缩略图地址（用于网格墙展示，有效期 2 小时）
                        thumbUrl = ossClient.GeneratePresignedUri(config.BucketName, $"{albumRecord.Id}/thumbs/{p}", DateTime.Now.AddHours(2)).ToString(),
                        // 高清大图展示地址（用于灯箱全屏，有效期 2 小时）
                        displayUrl = ossClient.GeneratePresignedUri(config.BucketName, $"{albumRecord.Id}/{p}", DateTime.Now.AddHours(2)).ToString(),
                        // 长效下载地址（供画廊客户下载原图使用，有效期 24 小时）
                        downloadUrl = ossClient.GeneratePresignedUri(config.BucketName, $"{albumRecord.Id}/{p}", DateTime.Now.AddHours(24)).ToString()
                    }).ToList<object>()
                    : new List<object>(); // 👈 选片通道关闭时，抛弃列表，物理切断数据外流

                // 1.4 下发组装好的数据模型给前端
                return Results.Ok(new
                {
                    id = albumRecord.Id,
                    name = albumRecord.Name,
                    canDownload = albumRecord.CanDownload,
                    canSelect = albumRecord.CanSelect, // 前端依靠此标志进行UI降级
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