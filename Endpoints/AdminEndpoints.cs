using Aliyun.OSS;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PhotographyWorkflow.Api.Models;
using System;
using System.Linq;

namespace PhotographyWorkflow.Api.Endpoints
{
    public static class AdminEndpoints
    {
        public static void MapAdminEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/admin");

            // ==========================================
            // ✨ 核心交付流线
            // ==========================================

            // 1. 创建发货相册
            group.MapPost("/albums", async (AlbumRequest req, AppDbContext db) =>
            {
                var albumName = string.IsNullOrWhiteSpace(req.Name)
                                ? $"{DateTime.Now:yyyyMMdd}_新相册"
                                : req.Name;

                DateTime? expiry = req.ExpiryDays switch
                {
                    -1 => null,
                    _ => DateTime.Now.AddDays(req.ExpiryDays)
                };

                var newAlbum = new Album
                {
                    Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                    Name = albumName,
                    PhoneNumber = req.PhoneNumber,
                    ExpiryDate = expiry
                };

                db.Albums.Add(newAlbum);
                await db.SaveChangesAsync();

                return Results.Ok(newAlbum);
            });

            // 2. 摄影师传图接口 (带 FromForm 标记)
            group.MapPost("/upload", async ([FromForm] IFormFile file, [FromForm] string albumId, IOptions<OssOptions> options, OssClient ossClient) =>
            {
                var config = options.Value;
                if (file == null || file.Length == 0) return Results.BadRequest("未收到有效照片");

                string cloudPath = $"{albumId}/{DateTime.Now:yyyyMMddHHmmss}_{file.FileName}";

                try
                {
                    using (var stream = file.OpenReadStream())
                    {
                        ossClient.PutObject(config.BucketName, cloudPath, stream);
                    }
                    return Results.Ok(new { path = cloudPath });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[上传故障] OSS 通讯异常: {ex.Message}");
                    return Results.Problem("上传失败");
                }
            }).DisableAntiforgery();

            // 3. 追加传图：校验相册并更新有效期
            group.MapPost("/prepare-append", async (AppendRequest req, AppDbContext db) =>
            {
                var album = db.Albums.FirstOrDefault(a => a.Name == req.Identifier || a.Id == req.Identifier);

                if (album == null)
                    return Results.NotFound(new { message = "未找到该相册，请检查名称或提取码" });

                if (req.ExpiryDays != -99)
                {
                    album.ExpiryDate = req.ExpiryDays == -1 ? null : DateTime.Now.AddDays(req.ExpiryDays);
                    await db.SaveChangesAsync();
                }

                return Results.Ok(album);
            });

            // 4. 自动查询接口：用于追加传图时的实时校验
            group.MapGet("/lookup", async (string identifier, AppDbContext db) =>
            {
                string code = identifier;
                if (identifier.Contains("?album="))
                {
                    code = identifier.Split("?album=")[1].Split('&')[0];
                }

                var album = await db.Albums.FindAsync(code);
                if (album == null) return Results.NotFound();

                return Results.Ok(new { album.Name, album.Id });
            });


            // ==========================================
            // 🗂️ 交付大盘管理模块
            // ==========================================

            // 5. 获取所有相册列表 (大盘数据源)
            group.MapGet("/all", async (AppDbContext db) => {
                var albums = await db.Albums.OrderByDescending(a => a.CreatedAt).ToListAsync();
                return Results.Ok(albums);
            });

            // 6. 到期隐私擦除 (只删照片，保留数据记录)
            group.MapPost("/archive/{id}", async (string id, IOptions<OssOptions> options, OssClient ossClient, AppDbContext db) => {
                var album = await db.Albums.FindAsync(id);
                if (album == null) return Results.NotFound();

                var config = options.Value;
                try
                {
                    var listRequest = new ListObjectsRequest(config.BucketName) { Prefix = $"{id}/" };
                    var list = ossClient.ListObjects(listRequest);
                    foreach (var obj in list.ObjectSummaries)
                    {
                        ossClient.DeleteObject(config.BucketName, obj.Key);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[清理 OSS 失败]: {ex.Message}");
                }

                album.Status = 2; // 2 代表已擦除
                await db.SaveChangesAsync();

                return Results.Ok();
            });

            // 7. 彻底物理删除 (连根拔起)
            group.MapDelete("/delete/{id}", async (string id, IOptions<OssOptions> options, OssClient ossClient, AppDbContext db) => {
                var album = await db.Albums.FindAsync(id);
                if (album == null) return Results.NotFound();

                var config = options.Value;
                try
                {
                    var listRequest = new ListObjectsRequest(config.BucketName) { Prefix = $"{id}/" };
                    var list = ossClient.ListObjects(listRequest);
                    foreach (var obj in list.ObjectSummaries)
                    {
                        ossClient.DeleteObject(config.BucketName, obj.Key);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[清理 OSS 失败]: {ex.Message}");
                }

                db.Albums.Remove(album);
                await db.SaveChangesAsync();

                return Results.Ok();
            });



            // 8. 获取某个相册的所有选片记录
            group.MapGet("/selections/{albumId}", async (string albumId, AppDbContext db) =>
            {
                var selections = await db.Selections
                    .Where(s => s.AlbumId == albumId)
                    .OrderByDescending(s => s.SubmitTime) // 按时间倒序，最新的在最上面
                    .ToListAsync();

                return Results.Ok(selections);
            });

            // 9. 更新相册的业务状态 (付款与下载)
            group.MapPost("/update-status", async (UpdateStatusRequest req, AppDbContext db) =>
            {
                var album = await db.Albums.FindAsync(req.Id);
                if (album == null) return Results.NotFound();

                if (req.Field == "IsPaid") album.IsPaid = req.Value;
                if (req.Field == "CanDownload") album.CanDownload = req.Value;

                await db.SaveChangesAsync();
                return Results.Ok();
            });

        }
    }

    // 💡 放在最底部的数据传输模型 (DTO)
    public record AlbumRequest(string? Name, string? PhoneNumber, int ExpiryDays);
    public record AppendRequest(string Identifier, int ExpiryDays);
    public record UpdateStatusRequest(string Id, string Field, bool Value);
}