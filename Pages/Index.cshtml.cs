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

            string endpoint = Environment.GetEnvironmentVariable("VISION_ENDPOINT");
            string key = Environment.GetEnvironmentVariable("VISION_KEY");

            var client = new ImageAnalysisClient(
                new Uri(endpoint),
                new AzureKeyCredential(key));

            ImageAnalysisResult analysisResult = await client.AnalyzeAsync(
                new BinaryData(imageData),                
                VisualFeatures.Caption | VisualFeatures.Read | VisualFeatures.Tags | VisualFeatures.Objects);

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
                    using (var canvas = new SKCanvas(originalImage))
                    {
                        if (analysisResult.Objects != null)
                        {
                            foreach (var detectedObject in analysisResult.Objects.Values)
                            {
                                var rect = detectedObject.BoundingBox;

                                // Fill paint with transparency
                                var fillPaint = new SKPaint
                                {
                                    Style = SKPaintStyle.Fill,
                                    Color = new SKColor(255, 0, 0, 70)
                                };

                                // Stroke paint for the border
                                var strokePaint = new SKPaint
                                {
                                    Style = SKPaintStyle.Stroke,
                                    Color = SKColors.Red,
                                    StrokeWidth = 2
                                };

                                canvas.DrawRect(rect.X, rect.Y, rect.Width, rect.Height, fillPaint);
                                canvas.DrawRect(rect.X, rect.Y, rect.Width, rect.Height, strokePaint);
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
