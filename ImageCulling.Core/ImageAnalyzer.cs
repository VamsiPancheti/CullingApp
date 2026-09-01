// File: ImageAnalyzer.cs
using System;
using System.IO;
using System.Linq;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using SkiaSharp;

namespace ImageCulling.Core;

public class ImageAnalyzer
{
   private const double IntentionalBlurThreshold = 0.02;
   private const double SharpnessRejectionThreshold = 70.0;
   private const double UnderexposedThreshold = 12.0;
   private const double OverexposedThreshold = 248.0;

   public PhotoMetadata AnalyzePhoto(string filePath, float[] vector, byte[] extractedJpegBytes)
   {
       var meta = new PhotoMetadata
       {
           FilePath = filePath,
           Vector = vector,
           ShotDate = DateTime.Now,
           ShutterSpeed = 0.01
       };

       try
       {
           var directories = ImageMetadataReader.ReadMetadata(filePath);
           var subIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
           if (subIfdDirectory != null)
           {
               if (subIfdDirectory.TryGetDouble(ExifDirectoryBase.TagExposureTime, out double shutter))
                   meta.ShutterSpeed = shutter;
               if (subIfdDirectory.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out DateTime date))
                   meta.ShotDate = date;
           }
       }
       catch { }

       var metrics = CalculateMetricsAndFaces(extractedJpegBytes);
       meta.SharpnessScore = metrics.sharpness;
       meta.ExposureScore = metrics.exposure;
       meta.FaceCount = metrics.faceCount;

       if (meta.ExposureScore < UnderexposedThreshold)
       {
           meta.IsRejected = true;
           meta.RejectionReason = $"Underexposed (Score: {meta.ExposureScore:F1})";
           return meta;
       }

       if (meta.ExposureScore > OverexposedThreshold)
       {
           meta.IsRejected = true;
           meta.RejectionReason = $"Overexposed (Score: {meta.ExposureScore:F1})";
           return meta;
       }

       if (meta.ShutterSpeed < IntentionalBlurThreshold)
       {
           if (meta.SharpnessScore < SharpnessRejectionThreshold && !meta.HasFaces)
           {
               meta.IsRejected = true;
               meta.RejectionReason = $"Unintentional Blur (Score: {meta.SharpnessScore:F1})";
           }
       }
       else
       {
           meta.SharpnessScore = -1;
       }

       return meta;
   }

   private (double sharpness, double exposure, int faceCount) CalculateMetricsAndFaces(byte[] jpegBytes)
   {
       if (jpegBytes == null || jpegBytes.Length == 0)
       {
           return (0.0, 0.0, 0);
       }

       try
       {
           using var codec = SKCodec.Create(new MemoryStream(jpegBytes));
           if (codec == null)
           {
               return (0.0, 0.0, 0);
           }

           using var bitmap = SKBitmap.Decode(codec);
           if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0)
           {
               return (0.0, 0.0, 0);
           }

           int detectedFaces = 0;
           if (!OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS() && !OperatingSystem.IsMacCatalyst())
           {
               try
               {
                   using var image = SKImage.FromBitmap(bitmap);
                   using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                   using var stream = data.AsStream();
                   using var detector = new FaceONNX.FaceDetector();
                   using var drawingBitmap = new System.Drawing.Bitmap(stream);
                   var faces = detector.Forward(drawingBitmap);
                   detectedFaces = faces?.Length ?? 0;
               }
               catch
               {
                   detectedFaces = 0;
               }
           }

           using var grayscale = new SKBitmap(bitmap.Width, bitmap.Height, SKColorType.Gray8, SKAlphaType.Opaque);
           bitmap.CopyTo(grayscale);

           int width = grayscale.Width;
           int height = grayscale.Height;

           if (width <= 2 || height <= 2)
           {
               return (0.0, 0.0, detectedFaces);
           }

           int startX = width / 4;
           int endX = width - startX;
           int startY = height / 4;
           int endY = height - startY;
           int evalWidth = endX - startX;
           int evalHeight = endY - startY;

           if (evalWidth <= 0 || evalHeight <= 0)
           {
               return (0.0, 0.0, detectedFaces);
           }

           double[] laplacian = new double[evalWidth * evalHeight];
           double sumLaplacian = 0.0;
           double sumBrightness = 0.0;
           int count = 0;

           unsafe
           {
               byte* src = (byte*)grayscale.GetPixels().ToPointer();
               for (int y = startY; y < endY; y++)
               {
                   for (int x = startX; x < endX; x++)
                   {
                       int idx = y * width + x;
                       byte pixelValue = src[idx];
                       sumBrightness += pixelValue;

                       double center = pixelValue * -4.0;
                       double top = src[(y - 1) * width + x];
                       double bottom = src[(y + 1) * width + x];
                       double left = src[idx - 1];
                       double right = src[idx + 1];

                       double lapVal = center + top + bottom + left + right;
                       int localIdx = (y - startY) * evalWidth + (x - startX);
                       laplacian[localIdx] = lapVal;
                       sumLaplacian += lapVal;
                       count++;
                   }
               }
           }

           if (count == 0)
           {
               return (0.0, 0.0, detectedFaces);
           }

           double meanLap = sumLaplacian / count;
           double meanBrightness = sumBrightness / count;
           double varianceSum = 0.0;

           for (int i = 0; i < laplacian.Length; i++)
           {
               if (laplacian[i] != 0.0)
               {
                   varianceSum += Math.Pow(laplacian[i] - meanLap, 2);
               }
           }

           return (varianceSum / count, meanBrightness, detectedFaces);
       }
       catch
       {
           return (0.0, 0.0, 0);
       }
   }
}