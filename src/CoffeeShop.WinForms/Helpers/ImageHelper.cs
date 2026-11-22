using System.Collections.Concurrent;
using System.Drawing.Drawing2D;
using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Helpers;

public static class ImageHelper
{
    private sealed record CacheEntry(Bitmap Bitmap, object SyncRoot);

    private static readonly ConcurrentDictionary<string, Lazy<CacheEntry>> _cache = new();
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    public static Image LoadProductImage(string? relativeOrFullPath, string fallbackTitle = "Coffee", int maxDimension = 320)
    {
        var resolved = ResolvePath(relativeOrFullPath);
        if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
        {
            long ticks;
            try
            {
                ticks = File.GetLastWriteTimeUtc(resolved).Ticks;
            }
            catch
            {
                ticks = 0;
            }

            var fileKey = $"file|{resolved}|{ticks}|{maxDimension}";
            var entry = GetOrAddEntry(fileKey, () =>
            {
                try
                {
                    var bytes = File.ReadAllBytes(resolved);
                    using var ms = new MemoryStream(bytes);
                    using var original = Image.FromStream(ms);
                    return ResizeToFit(original, maxDimension);
                }
                catch
                {
                    return CreatePlaceholder(fallbackTitle, maxDimension);
                }
            });

            lock (entry.SyncRoot)
                return new Bitmap(entry.Bitmap);
        }

        var placeholderKey = $"placeholder|{fallbackTitle}|{maxDimension}";
        var placeholderEntry = GetOrAddEntry(placeholderKey, () => CreatePlaceholder(fallbackTitle, maxDimension));
        lock (placeholderEntry.SyncRoot)
            return new Bitmap(placeholderEntry.Bitmap);
    }

    public static Image? LoadLogo(int maxDimension = 140)
    {
        var path = ResolvePath("Assets/Images/logo_coffee.png");
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            var ticks = File.GetLastWriteTimeUtc(path).Ticks;
            var key = $"logo|{path}|{ticks}|{maxDimension}";
            var entry = _cache.GetOrAdd(key, _ => new Lazy<CacheEntry>(() =>
            {
                var bytes = File.ReadAllBytes(path);
                using var ms = new MemoryStream(bytes);
                using var original = Image.FromStream(ms);
                return new CacheEntry(ResizeToFit(original, maxDimension), new object());
            })).Value;

            lock (entry.SyncRoot)
                return new Bitmap(entry.Bitmap);
        }
        catch
        {
            return null;
        }
    }

    public static Task PreloadProductImagesAsync(IEnumerable<(string? ImagePath, string FallbackTitle)> items, int maxDimension = 160)
    {
        var distinct = items
            .GroupBy(x => (Path: x.ImagePath ?? string.Empty, Title: x.FallbackTitle))
            .Select(g => g.First())
            .ToList();

        if (distinct.Count == 0)
            return Task.CompletedTask;

        return Task.Run(() =>
        {
            Parallel.ForEach(distinct, item =>
            {
                try
                {
                    using var _ = LoadProductImage(item.ImagePath, item.FallbackTitle, maxDimension);
                }
                catch
                {
                    // Ignore bad or missing paths during warm-up
                }
            });
        });
    }

    public static Task PreloadLogoAsync(int maxDimension = 120)
    {
        return Task.Run(() =>
        {
            try
            {
                using var _ = LoadLogo(maxDimension);
            }
            catch
            {
                // Ignore missing or bad logo during warm-up
            }
        });
    }

    public static async Task<Image?> LoadVietQrAsync(
        CoffeeShop.Application.Common.AppSettings settings,
        decimal amount,
        string memo,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.BankCode) || string.IsNullOrWhiteSpace(settings.BankAccountNo))
            return null;

        var roundedAmount = Math.Max(0L, (long)Math.Round(amount, MidpointRounding.AwayFromZero));
        var url =
            $"https://img.vietqr.io/image/{Uri.EscapeDataString(settings.BankCode.Trim())}-{Uri.EscapeDataString(settings.BankAccountNo.Trim())}-compact.png" +
            $"?amount={roundedAmount}&addInfo={Uri.EscapeDataString(memo ?? string.Empty)}&accountName={Uri.EscapeDataString(settings.BankAccountName ?? string.Empty)}";

        var cacheKey = $"vietqr|{url}";
        if (_cache.TryGetValue(cacheKey, out var existing))
        {
            lock (existing.Value.SyncRoot)
                return new Bitmap(existing.Value.Bitmap);
        }

        try
        {
            var bytes = await _httpClient.GetByteArrayAsync(url, ct).ConfigureAwait(false);
            var entry = GetOrAddEntry(cacheKey, () =>
            {
                using var ms = new MemoryStream(bytes);
                using var original = Image.FromStream(ms);
                return ResizeToFit(original, 320);
            });

            lock (entry.SyncRoot)
                return new Bitmap(entry.Bitmap);
        }
        catch
        {
            return null;
        }
    }

    public static string? CopyImageToAssets(string sourceFilePath, int categoryId)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            return null;

        var subFolder = categoryId == 1 ? "drink" : "food";
        var targetDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Images", subFolder);
        Directory.CreateDirectory(targetDir);

        var ext = Path.GetExtension(sourceFilePath).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".bmp"))
            ext = ".jpg";

        var fileName = $"{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}{ext}";
        var targetPath = Path.Combine(targetDir, fileName);
        File.Copy(sourceFilePath, targetPath, overwrite: true);

        return $"Assets/Images/{subFolder}/{fileName}";
    }

    private static CacheEntry GetOrAddEntry(string key, Func<Bitmap> bitmapFactory)
    {
        var lazy = _cache.GetOrAdd(key, _ => new Lazy<CacheEntry>(
            () => new CacheEntry(bitmapFactory(), new object()),
            LazyThreadSafetyMode.ExecutionAndPublication));
        return lazy.Value;
    }

    private static string? ResolvePath(string? relativeOrFullPath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrFullPath))
            return null;

        if (Path.IsPathRooted(relativeOrFullPath))
            return relativeOrFullPath;

        var normalized = relativeOrFullPath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(AppContext.BaseDirectory, normalized);
    }

    private static Bitmap ResizeToFit(Image source, int maxDimension)
    {
        var ratio = Math.Min((double)maxDimension / source.Width, (double)maxDimension / source.Height);
        if (ratio >= 1.0)
            return new Bitmap(source);

        var newW = Math.Max(1, (int)Math.Round(source.Width * ratio));
        var newH = Math.Max(1, (int)Math.Round(source.Height * ratio));

        var bmp = new Bitmap(newW, newH);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(source, 0, 0, newW, newH);
        return bmp;
    }

    private static Bitmap CreatePlaceholder(string title, int size)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(CoffeeTheme.Cream);

        using var brush = new SolidBrush(CoffeeTheme.Latte);
        g.FillEllipse(brush, size / 4, size / 5, size / 2, size / 2);

        using var textBrush = new SolidBrush(CoffeeTheme.Mocha);
        using var font = new Font("Segoe UI", 11F, FontStyle.Bold);
        var initials = string.IsNullOrWhiteSpace(title)
            ? "CF"
            : string.Concat(title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(initials, font, textBrush, new RectangleF(0, 0, size, size), sf);

        return bmp;
    }
}
