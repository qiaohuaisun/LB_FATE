using Microsoft.Maui.Layouts;
using System.Diagnostics;

namespace LB_FATE.Mobile.Views;

public partial class GamePage
{
    // 优化后的高性能打字机台词展示
    public async Task ShowBossQuoteOptimizedAsync(string quote, string eventType)
    {
        if (BossQuoteLayer == null || string.IsNullOrWhiteSpace(quote))
            return;

        // UI 去重：短时间内相同台词不重复
        var now = DateTime.Now;
        var timeSinceLast = now - _lastQuoteTime;
        if (_lastQuote == quote && timeSinceLast.TotalMilliseconds < 3000)
            return;

        _lastQuote = quote;
        _lastQuoteTime = now;

        // 文本颜色按事件类型区分
        Color textColor = eventType switch
        {
            "turn_start" => Color.FromArgb("#FF6B6B"),
            "turn_end" => Color.FromArgb("#FF6B6B"),
            "skill" => Color.FromArgb("#DA70D6"),
            "hp_threshold" => Color.FromArgb("#FF4500"),
            _ => Color.FromArgb("#FFD700")
        };

        // 字号
        double fontSize = DeviceInfo.Platform == DevicePlatform.Android ? 16 : 24;

        // 随机位置，保证与上次位置有足够距离
        double xPercent, yPercent;
        int retry = 0, maxRetry = 10;
        do
        {
            if (_isLandscape)
            {
                xPercent = _random.NextDouble() * 0.9 + 0.05; // 5%~95%
                yPercent = _random.NextDouble() * 0.8 + 0.10; // 10%~90%
            }
            else
            {
                xPercent = _random.NextDouble() * 0.8 + 0.10; // 10%~90%
                yPercent = _random.NextDouble() * 0.7 + 0.10; // 10%~80%
            }

            if (_lastQuotePosition is { } last)
            {
                double dx = xPercent - last.x;
                double dy = yPercent - last.y;
                if (Math.Sqrt(dx * dx + dy * dy) >= 0.25) break;
            }
            else break;
            retry++;
        } while (retry < maxRetry);

        _lastQuotePosition = (xPercent, yPercent);

        // 轻微旋转
        double maxRotation = DeviceInfo.Platform == DevicePlatform.Android ? 28 : 22;
        double rotation = (_random.NextDouble() - 0.5) * maxRotation * 2;

        // 用完整文本先测量尺寸并固定，以避免逐字过程中的布局抖动
        var labelForMeasure = new Label
        {
            Text = quote,
            TextColor = textColor,
            FontSize = fontSize,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Colors.Transparent,
            Padding = new Thickness(0),
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            Shadow = new Shadow { Brush = Colors.Black, Opacity = 0.8f, Radius = 8, Offset = new Point(2, 2) }
        };
        // 通过测量控件得到期望尺寸
        var measured = labelForMeasure.Measure(double.PositiveInfinity, double.PositiveInfinity);

        // 实际展示用的 Label（初始空文本）
        var label = new Label
        {
            Text = string.Empty,
            TextColor = textColor,
            FontSize = fontSize,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Colors.Transparent,
            Padding = new Thickness(0),
            Opacity = 0,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            Shadow = new Shadow { Brush = Colors.Black, Opacity = 0.8f, Radius = 8, Offset = new Point(2, 2) }
        };

        AbsoluteLayout.SetLayoutFlags(label, AbsoluteLayoutFlags.PositionProportional);
        AbsoluteLayout.SetLayoutBounds(label, new Rect(xPercent, yPercent, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
        label.Rotation = rotation;

        await MainThread.InvokeOnMainThreadAsync(() => BossQuoteLayer.Children.Add(label));

        // 取消前一条台词动画，创建新的取消源
        _quoteCts?.Cancel();
        _quoteCts = new CancellationTokenSource();
        var ct = _quoteCts.Token;

        // 入场动画（并行进行打字）
        label.Scale = 0.92;
        _ = MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Task.WhenAll(
                label.FadeToAsync(1.0, 140, Easing.CubicOut),
                label.ScaleToAsync(1.0, 160, Easing.CubicOut)
            );
        });

        // 帧驱动逐字显示
        var sw = Stopwatch.StartNew();
        // Slow down typing so the effect is actually visible on devices
        // Android phones a bit faster, desktop slightly slower for readability
        int cps = DeviceInfo.Platform == DevicePlatform.Android ? 22 : 18; // chars per second

        // Manual typewriter loop for better cross-platform reliability
        while (!ct.IsCancellationRequested)
        {
            int target = Math.Min(quote.Length, (int)(sw.Elapsed.TotalSeconds * cps));
            string text = target > 0 ? quote.Substring(0, target) : string.Empty;
            await MainThread.InvokeOnMainThreadAsync(() => label.Text = text);
            if (target >= quote.Length) break;
            try { await Task.Delay(16, ct); } catch (TaskCanceledException) { }
        }
        if (ct.IsCancellationRequested)
        {
            await MainThread.InvokeOnMainThreadAsync(() => BossQuoteLayer.Children.Remove(label));
            return;
        }

        // 打完字后保证停留（包含已用时间），然后淡出
        int baseMs = 1200, perCharMs = 35, maxMs = 3000;
        int targetTotal = Math.Min(maxMs, baseMs + Math.Min(quote.Length, 80) * perCharMs);
        int typedMs = (int)sw.ElapsedMilliseconds;
        int holdMs = Math.Max(900, targetTotal - typedMs);
        if (!ct.IsCancellationRequested && holdMs > 0)
            await Task.Delay(holdMs);

        if (!ct.IsCancellationRequested)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await label.FadeToAsync(0.0, 220, Easing.CubicIn);
                BossQuoteLayer.Children.Remove(label);
            });
        }
    }
}
