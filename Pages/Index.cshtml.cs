using Azure;
using Azure.AI.Vision.ImageAnalysis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DotNetEnv;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ImageAnalysisApp.Pages
{
    public class UploadModel : PageModel
    {
        [BindProperty]
        public IFormFile ImageFile { get; set; }

        public string Caption { get; set; }
        public List<string> Tags { get; set; }
        public string ThumbnailImage { get; set; }


        public async Task<IActionResult> OnPostAsync()
        {
            if (ImageFile == null || ImageFile.Length == 0)
            {
                ModelState.AddModelError(string.Empty, "Please select a valid image file.");
                return Page();
            }

            // Read the image file into a byte array
            byte[] imageData;
            using (var memoryStream = new MemoryStream())
            {
                await ImageFile.CopyToAsync(memoryStream);
                imageData = memoryStream.ToArray();
            }

            Env.Load();

            // Get environment variables
            string endpoint = Environment.GetEnvironmentVariable("VISION_ENDPOINT");
            string key = Environment.GetEnvironmentVariable("VISION_KEY");

            // Create the Image Analysis client
            var client = new ImageAnalysisClient(
                new Uri(endpoint),
                new AzureKeyCredential(key));

            // Analyze the image
            ImageAnalysisResult analysisResult = await client.AnalyzeAsync(
                new BinaryData(imageData),                
                VisualFeatures.Caption | VisualFeatures.Read | VisualFeatures.Tags | VisualFeatures.Objects);

            // Get the tags from the analysis result
            Tags = new List<string>();
            foreach (var tag in analysisResult.Tags.Values)
            {
                Tags.Add(tag.Name + ' ' + Math.Round(tag.Confidence * 100, 2) + '%');
            }

            Caption = analysisResult.Caption.Text;

            // Generate a thumbnail with bounding boxes using SkiaSharp
            using (var inputStream = new SKMemoryStream(imageData))
            {
                using (var originalImage = SKBitmap.Decode(inputStream))
                {
                    // const int thumbnailSize = 500;
                    // var thumbnail = originalImage.Resize(new SKImageInfo(thumbnailSize, thumbnailSize), SKFilterQuality.High);

                    using (var canvas = new SKCanvas(originalImage))
                    {
                        if (analysisResult.Objects != null)
                        {
                            foreach (var detectedObject in analysisResult.Objects.Values)
                            {
                                var rect = detectedObject.BoundingBox;

                                // // Calculate bounding box coordinates for thumbnail
                                // var left = rect.X * thumbnail.Width / originalImage.Width;
                                // var top = rect.Y * thumbnail.Height / originalImage.Height;
                                // var width = rect.Width * thumbnail.Width / originalImage.Width;
                                // var height = rect.Height * thumbnail.Height / originalImage.Height;

                                var left = rect.X;
                                var top = rect.Y;
                                var width = rect.Width;
                                var height = rect.Height;

                                // Fill paint with transparency
                                var fillPaint = new SKPaint
                                {
                                    Style = SKPaintStyle.Fill,
                                    Color = new SKColor(255, 0, 0, 70) // Red color with transparency (alpha = 100)
                                };

                                // Stroke paint for the border
                                var strokePaint = new SKPaint
                                {
                                    Style = SKPaintStyle.Stroke,
                                    Color = SKColors.Red,
                                    StrokeWidth = 2
                                };

                                // Draw filled rectangle
                                canvas.DrawRect(left, top, width, height, fillPaint);

                                // Draw rectangle border
                                canvas.DrawRect(left, top, width, height, strokePaint);
                            }
                        }

                        using (var image = SKImage.FromBitmap(originalImage))
                        using (var outputStream = new MemoryStream())
                        {
                            image.Encode(SKEncodedImageFormat.Png, 100).SaveTo(outputStream);
                            ThumbnailImage = Convert.ToBase64String(outputStream.ToArray());
                        }
                    }
                }
            }

            return Page();
        }
    }
}
