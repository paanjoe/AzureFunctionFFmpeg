using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureFunctionFFmpeg;

public static class ResizeImage
{
    //You must add the ffmpeg.exe file to the same directory as the ResizeImage.cs file.
    private static string ffmpegPath = "/home/site/wwwroot/ffmpeg.exe";
    private static string tempPath = null;

    [FunctionName("ResizeImage")]
    public static async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)]
        HttpRequest req, ILogger log)
    {
        try
        {
            //c: or d: ?
            if (ffmpegPath.StartsWith('/'))
            {
                var pathStart = Directory.GetCurrentDirectory().Split('\\')[0];
                ffmpegPath = pathStart + ffmpegPath;
            }

            if (tempPath == null)
                tempPath = Path.GetTempPath();

            var file = req.Form.Files[0];
            var width = "-1";
            var height = "-1";
            width = req.Form.ContainsKey("width") ? req.Form["width"] : "-1";
            height = req.Form.ContainsKey("height") ? req.Form["height"] : "-1";

            //create tmpImage file
            using (Stream tmpImage = new FileStream($"{tempPath}{file.FileName}", FileMode.Create))
            {
                await file.CopyToAsync(tmpImage);
            }

            var cliCode =
                $"-i {tempPath}{file.FileName} -vf scale={width}:{height} {tempPath}new-{file.FileName}";
            var psi = new ProcessStartInfo();
            psi.FileName = ffmpegPath;
            psi.Arguments = cliCode;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;
            var process = Process.Start(psi);
            process.WaitForExit((int) TimeSpan.FromSeconds(60).TotalMilliseconds);


            //get image bytes
            byte[] byteArray;
            using (var fileStream = File.Open($"{tempPath}/new-{file.FileName}", FileMode.Open))
            {
                var memoryStream = new MemoryStream();
                fileStream.CopyTo(memoryStream);
                byteArray = memoryStream.ToArray();
            }

            //delete temp files
            File.Delete($"{tempPath}{file.FileName}");
            File.Delete($"{tempPath}new-{file.FileName}");

            return new FileContentResult(byteArray, GetContentType(file.FileName));
        }
        catch (Exception e)
        {
            return new BadRequestObjectResult(e);
        }
    }
    private static string GetContentType(string extension)
    {
        extension = extension.Split('.')[1];
        switch (extension.ToLower())
        {
            case "png":
                return "image/png";
            case "jpg":
            case "jpeg":
                return "image/jpeg";
            case "gif":
                return "image/gif";
            default:
                return "image/jpeg";
        }
    }
}