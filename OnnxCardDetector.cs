using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PokerScreenScraper;

internal static class OnnxCardDetector
{
    private const int InputSize = 640;
    private const float MinConfidence = 0.24F;
    private const float NmsThreshold = 0.38F;
    private static readonly Lazy<ModelState?> State = new(LoadModel);

    public static bool IsAvailable => State.Value is not null;

    public static IReadOnlyList<CardDetection> Detect(Bitmap image)
    {
        var state = State.Value;
        if (state is null || image.Width <= 0 || image.Height <= 0)
        {
            return Array.Empty<CardDetection>();
        }

        try
        {
            using var input = PrepareInput(image);
            var inputName = state.Session.InputMetadata.Keys.First();
            using var results = state.Session.Run(new[]
            {
                NamedOnnxValue.CreateFromTensor(inputName, input.Tensor)
            });

            var output = results.First().AsTensor<float>();
            var detections = ParseOutput(output, state.Labels, input.Scale, input.PaddingX, input.PaddingY, image.Size);
            return ApplyNms(detections);
        }
        catch
        {
            return Array.Empty<CardDetection>();
        }
    }

    private static ModelState? LoadModel()
    {
        var baseDir = AppContext.BaseDirectory;
        var modelPath = Path.Combine(baseDir, "models", "card-detector.onnx");
        var labelsPath = Path.Combine(baseDir, "models", "labels.txt");

        if (!File.Exists(modelPath) || !File.Exists(labelsPath))
        {
            return null;
        }

        var labels = File.ReadAllLines(labelsPath)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (labels.Length == 0)
        {
            return null;
        }

        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            InterOpNumThreads = 1,
            IntraOpNumThreads = 1,
        };

        return new ModelState(new InferenceSession(modelPath, options), labels);
    }

    private static PreparedInput PrepareInput(Bitmap image)
    {
        var scale = Math.Min(InputSize / (float)image.Width, InputSize / (float)image.Height);
        var resizedWidth = Math.Max(1, (int)Math.Round(image.Width * scale));
        var resizedHeight = Math.Max(1, (int)Math.Round(image.Height * scale));
        var padX = (InputSize - resizedWidth) / 2;
        var padY = (InputSize - resizedHeight) / 2;

        var tensor = new DenseTensor<float>(new[] { 1, 3, InputSize, InputSize });
        using var canvas = new Bitmap(InputSize, InputSize);
        using (var g = Graphics.FromImage(canvas))
        {
            g.Clear(Color.FromArgb(114, 114, 114));
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(image, new Rectangle(padX, padY, resizedWidth, resizedHeight));
        }

        var data = canvas.LockBits(new Rectangle(0, 0, canvas.Width, canvas.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[Math.Abs(data.Stride) * data.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

            for (var y = 0; y < InputSize; y++)
            {
                var row = y * data.Stride;
                for (var x = 0; x < InputSize; x++)
                {
                    var index = row + x * 4;
                    tensor[0, 0, y, x] = bytes[index + 2] / 255F;
                    tensor[0, 1, y, x] = bytes[index + 1] / 255F;
                    tensor[0, 2, y, x] = bytes[index] / 255F;
                }
            }
        }
        finally
        {
            canvas.UnlockBits(data);
        }

        return new PreparedInput(tensor, scale, padX, padY);
    }

    private static List<CardDetection> ParseOutput(
        Tensor<float> output,
        string[] labels,
        float scale,
        int padX,
        int padY,
        Size originalSize)
    {
        var dims = output.Dimensions.ToArray();
        if (dims.Length != 3)
        {
            return new List<CardDetection>();
        }

        var a = dims[1];
        var b = dims[2];
        var yoloV8Transposed = a < b;
        var boxes = yoloV8Transposed ? b : a;
        var attributes = yoloV8Transposed ? a : b;
        var detections = new List<CardDetection>();

        for (var i = 0; i < boxes; i++)
        {
            float GetAttr(int attr) => yoloV8Transposed ? output[0, attr, i] : output[0, i, attr];

            if (attributes < 5)
            {
                continue;
            }

            var cx = GetAttr(0);
            var cy = GetAttr(1);
            var w = GetAttr(2);
            var h = GetAttr(3);
            var hasObjectness = attributes == labels.Length + 5 || attributes > labels.Length + 4;
            var objectness = hasObjectness ? NormalizeScore(GetAttr(4)) : 1F;
            var classOffset = hasObjectness ? 5 : 4;

            var bestClass = -1;
            var bestScore = 0F;
            var maxClass = Math.Min(labels.Length, attributes - classOffset);
            for (var c = 0; c < maxClass; c++)
            {
                var score = NormalizeScore(GetAttr(classOffset + c)) * objectness;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestClass = c;
                }
            }

            if (bestClass < 0 || bestScore < MinConfidence)
            {
                continue;
            }

            if (Math.Max(Math.Max(Math.Abs(cx), Math.Abs(cy)), Math.Max(Math.Abs(w), Math.Abs(h))) <= 2F)
            {
                cx *= InputSize;
                cy *= InputSize;
                w *= InputSize;
                h *= InputSize;
            }

            var left = (cx - w / 2F - padX) / scale;
            var top = (cy - h / 2F - padY) / scale;
            var right = (cx + w / 2F - padX) / scale;
            var bottom = (cy + h / 2F - padY) / scale;

            var rect = RectangleF.Intersect(
                RectangleF.FromLTRB(left, top, right, bottom),
                new RectangleF(0, 0, originalSize.Width, originalSize.Height));

            if (rect.Width <= 2 || rect.Height <= 2)
            {
                continue;
            }

            var label = NormalizeCardLabel(labels[bestClass]);
            if (label is null)
            {
                continue;
            }

            detections.Add(new CardDetection(label, rect, bestScore));
        }

        return detections;
    }

    private static IReadOnlyList<CardDetection> ApplyNms(List<CardDetection> detections)
    {
        var result = new List<CardDetection>();
        foreach (var candidate in detections.OrderByDescending(d => d.Confidence))
        {
            if (result.Any(existing => IoU(existing.Box, candidate.Box) > NmsThreshold))
            {
                continue;
            }

            result.Add(candidate);
        }

        return result;
    }

    private static string? NormalizeCardLabel(string value)
    {
        var clean = value.Trim().ToLowerInvariant()
            .Replace("10", "t", StringComparison.OrdinalIgnoreCase)
            .Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);

        if (clean.Length < 2)
        {
            return null;
        }

        var rank = clean[0] switch
        {
            'a' => "A",
            'k' => "K",
            'q' => "Q",
            'j' => "J",
            't' => "T",
            >= '2' and <= '9' => clean[0].ToString(),
            _ => null
        };

        var suit = clean[^1] switch
        {
            'h' => "h",
            'd' => "d",
            's' => "s",
            'c' => "c",
            _ => null
        };

        return rank is null || suit is null ? null : $"{rank}{suit}";
    }

    private static float NormalizeScore(float value)
    {
        if (value is >= 0F and <= 1F)
        {
            return value;
        }

        return 1F / (1F + MathF.Exp(-value));
    }

    private static float IoU(RectangleF a, RectangleF b)
    {
        var intersection = RectangleF.Intersect(a, b);
        if (intersection.Width <= 0 || intersection.Height <= 0)
        {
            return 0F;
        }

        var interArea = intersection.Width * intersection.Height;
        var unionArea = a.Width * a.Height + b.Width * b.Height - interArea;
        return unionArea <= 0 ? 0 : interArea / unionArea;
    }

    private sealed record ModelState(InferenceSession Session, string[] Labels);

    private sealed record PreparedInput(DenseTensor<float> Tensor, float Scale, int PaddingX, int PaddingY) : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

internal sealed record CardDetection(string Card, RectangleF Box, float Confidence);
