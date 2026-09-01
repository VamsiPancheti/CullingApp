// File: ClipEngine.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace ImageCulling.Core;

public class ClipEngine : IDisposable
{
    private readonly InferenceSession _session;
    private const int TargetSize = 224; // CLIP expected size

    public  ClipEngine(string modelPath)
    {
        var options = new SessionOptions();

        // Use DirectML for GPU acceleration on Windows (falls back gracefully to CPU if unsupported)
        try
        {
            options.AppendExecutionProvider_DML(0);
        }
        catch (Exception)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Warning: DirectML GPU engine unavailable. Falling back to CPU processing.");
            Console.ResetColor();
        }

        _session = new InferenceSession(modelPath, options);
    }

    public  float[] GenerateEmbedding(byte[] jpegData)
    {
        float[] normalizedPixels = PreprocessImage(jpegData);

        // Model shape input: [BatchSize (1), Channels (3), Height (224), Width (224)]
        var tensor = new DenseTensor<float>(normalizedPixels, new[] { 1, 3, TargetSize, TargetSize });

        var inputs = new List<NamedOnnxValue>
        {
            // Note: Replace "input" with your specific model's input name if different
            NamedOnnxValue.CreateFromTensor("images", tensor)
        };

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);

        // Extract output tensor (float array of 512 dimensions)
        float[] output = results.First().AsTensor<float>().ToArray();
        return L2Normalize(output);
    }

    private  float[] PreprocessImage(byte[] jpegData)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(jpegData));
        if (codec == null) throw new ArgumentException("Failed to decode image data.");

        // Decode original dimensions
        using var original = SKBitmap.Decode(codec);

        // Square-crop and resize image to 224x224
        using var resized = new SKBitmap(TargetSize, TargetSize);
        int minDim = Math.Min(original.Width, original.Height);
        var cropRect = new SKRectI(
            (original.Width - minDim) / 2,
            (original.Height - minDim) / 2,
            (original.Width + minDim) / 2,
            (original.Height + minDim) / 2
        );

        using var cropped = new SKBitmap(minDim, minDim);
        original.ExtractSubset(cropped, cropRect);
        cropped.ScalePixels(resized, SKFilterQuality.Medium);

        // Normalize color channels: subtract ImageNet means and divide by standard deviations
        // CLIP expected: Mean [0.48145466, 0.4578275, 0.40821073], StdDev [0.26862954, 0.26130258, 0.2757771]
        float[] normalizedData = new float[1 * 3 * TargetSize * TargetSize];

        int channelSize = TargetSize * TargetSize;
        int rOffset = 0;
        int gOffset = channelSize;
        int bOffset = channelSize * 2;

        for (int y = 0; y < TargetSize; y++)
        {
            for (int x = 0; x < TargetSize; x++)
            {
                SKColor color = resized.GetPixel(x, y);
                int index = y * TargetSize + x;

                // Normalize R, G, B channels and write directly to planar array layout
                normalizedData[rOffset + index] = (((color.Red / 255.0f) - 0.48145466f) / 0.26862954f);
                normalizedData[gOffset + index] = (((color.Green / 255.0f) - 0.4578275f) / 0.26130258f);
                normalizedData[bOffset + index] = (((color.Blue / 255.0f) - 0.40821073f) / 0.2757771f);
            }
        }

        return normalizedData;
    }

    private  float[] L2Normalize(float[] vector)
    {
        double sum = vector.Sum(v => v * v);
        float length = (float)Math.Sqrt(sum);
        return vector.Select(v => v / length).ToArray();
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
