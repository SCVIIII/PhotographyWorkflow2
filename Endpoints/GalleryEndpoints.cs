using Aliyun.OSS;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
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
            var group = app.MapGroup("/api/photos");

            // 1. 获取画廊照片列表 (💡 增加 AppDbContext db 参数，并改变返回结构)
            group.MapGet("/list", async (string album, AppDbContext db, IOptions<OssOptions> options, OssClient ossClient) =>
            {
                var albumRecord = await db.Albums.FindAsync(album);
                if (albumRecord == null) return Results.NotFound();

                var config = options.Value;
                var listRequest = new ListObjectsRequest(config.BucketName) { Prefix = $"{album}/" };
                var result = ossClient.ListObjects(listRequest);

                var photos = result.ObjectSummaries
                    .Where(obj => !obj.Key.EndsWith("/"))
                    .Select(obj =>
                    {
                        var thumbReq = new GeneratePresignedUriRequest(config.BucketName, obj.Key, SignHttpMethod.Get)
                        { Expiration = DateTime.Now.AddHours(2), Process = "image/resize,m_lfit,w_600/quality,q_80" };

                        var displayReq = new GeneratePresignedUriRequest(config.BucketName, obj.Key, SignHttpMethod.Get)
                        { Expiration = DateTime.Now.AddHours(2), Process = "image/resize,m_lfit,w_1920/quality,q_95" };

                        var downloadReq = new GeneratePresignedUriRequest(config.BucketName, obj.Key, SignHttpMethod.Get)
                        {
                            Expiration = DateTime.Now.AddHours(2),
                            ResponseHeaders = new ResponseHeaderOverrides { ContentDisposition = $"attachment; filename={Path.GetFileName(obj.Key)}" }
                        };

                        return new
                        {
                            key = obj.Key,
                            thumbUrl = ossClient.GeneratePresignedUri(thumbReq).ToString(),
                            displayUrl = ossClient.GeneratePresignedUri(displayReq).ToString(),
                            downloadUrl = ossClient.GeneratePresignedUri(downloadReq).ToString()
                        };
                    });

                // 💡 核心改变：不仅返回照片，还把相册的下载权限状态传给前端
                return Results.Ok(new
                {
                    name = albumRecord.Name,
                    canDownload = albumRecord.CanDownload,
                    photos = photos
                });
            });

            // 2. 一键打包下载
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


            // 3. 接收并保存客户的选片清单
            group.MapPost("/submit-selection", async (PhotoSelection payload, AppDbContext db) =>
            {
                // 自动打上当前的系统时间
                payload.SubmitTime = DateTime.Now;
                // 默认状态为未处理（摄影师还没看）
                payload.IsProcessed = false;

                db.Selections.Add(payload);
                await db.SaveChangesAsync();

                return Results.Ok(new { message = "选片清单已成功保存" });
            });


        }
    }
}