// File: RawExtractor.cs
using System;
using System.IO;
using Sdcb.LibRaw;
using SkiaSharp;

namespace ImageCulling.Core;

public class RawExtractor
{
    public byte[] ExtractEmbeddedJpeg(string rawFilePath)
    {
        if (!File.Exists(rawFilePath))
        {
            return null; // Return null gracefully instead of crashing
        }

        try
        {
            // OpenFile handles the native C binding
            using RawContext context = RawContext.OpenFile(rawFilePath);

            // ExportThumbnail safely handles the Unpack process internally
            using ProcessedImage thumbnail = context.ExportThumbnail(thumbnailIndex: 0);
            byte[] rawBytes = thumbnail.GetData<byte>().ToArray();

            // CRITICAL FIX: Downsample the image immediately to prevent MAUI OOM crashes
            return DownsampleImage(rawBytes, 250);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RawExtractor] Failed to extract {Path.GetFileName(rawFilePath)}: {ex.Message}");
            return null; 
        }
    }

    private byte[] DownsampleImage(byte[] imageBytes, int targetWidth)
    {
        try 
        {
            using var codec = SKCodec.Create(new SKMemoryStream(imageBytes));
            if (codec == null) return imageBytes; // Fallback to original if decoding fails

            var info = codec.Info;
            int targetHeight = (int)((float)targetWidth / info.Width * info.Height);
            var resizedInfo = new SKImageInfo(targetWidth, targetHeight);

            using var bitmap = SKBitmap.Decode(codec);
            using var resized = bitmap.Resize(resizedInfo, SKFilterQuality.Low);
            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 75);
            
            return data.ToArray();
        }
        catch 
        {
            return imageBytes; // Return original if Skia fails
        }
    }
}