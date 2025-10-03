using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Devices;

namespace LB_FATE.Mobile.Services;

public class DialogService
{
    private static Page? CurrentPage
    {
        get
        {
            var app = Application.Current;
            if (app == null) return null;

            // 优先使用 Windows[0].Page（新 API）
            var window = app.Windows?.FirstOrDefault();
            if (window?.Page != null)
                return window.Page;

            // 如果是 Shell，获取当前页面
            if (window?.Page is Shell shell && shell.CurrentPage != null)
                return shell.CurrentPage;

            return null;
        }
    }

    // Display a simple alert (single button)
    public async Task AlertAsync(string title, string message, string accept = "确定")
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = CurrentPage;
            if (page == null) return;
            await page.DisplayAlertAsync(title, message, accept);
        });
    }

    // Display an action sheet and return the selected option
    public async Task<string?> ActionSheetAsync(string title, string? cancel = "取消", string? destruction = null, params string[] buttons)
    {
        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = CurrentPage;
            if (page == null) return null;
            return await page.DisplayActionSheetAsync(title, cancel, destruction, buttons);
        });
    }

    // Display a prompt dialog and return the input text
    public async Task<string?> PromptAsync(string title, string message, string accept = "确定", string cancel = "取消", string? placeholder = null)
    {
        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = CurrentPage;
            if (page == null) return null;
            return await page.DisplayPromptAsync(title, message, accept, cancel, placeholder);
        });
    }

    // Show a Toolkit Popup and return the result
    public async Task<object?> ShowPopupAsync(Popup popup)
    {
        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = CurrentPage; if (page == null) return null;
            return await page.ShowPopupAsync(popup);
        });
    }

    // Display long multi-line text in a scrollable popup to avoid truncation
    public async Task ShowLongTextAsync(string title, string text, string accept = "关闭")
    {
        // On Android, prefer system alert dialog for reliability
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            await AlertAsync(title, text, accept);
            return;
        }

        try
        {
            var popup = new CommunityToolkit.Maui.Views.Popup
            {
                CanBeDismissedByTappingOutsideOfPopup = true
            };

            var container = new Grid
            {
                Padding = 16,
                BackgroundColor = Colors.White,
                RowDefinitions = new RowDefinitionCollection
                {
                    new RowDefinition(GridLength.Auto),
                    new RowDefinition(GridLength.Star),
                    new RowDefinition(GridLength.Auto)
                },
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star)
                }
            };

            var titleLabel = new Label
            {
                Text = title,
                FontAttributes = FontAttributes.Bold,
                FontSize = 18,
                TextColor = Color.FromArgb("#1F2328")
            };
            container.Add(titleLabel);
            Grid.SetRow(titleLabel, 0);

            var scroll = new ScrollView
            {
                Content = new Label
                {
                    Text = text,
                    FontSize = 14,
                    TextColor = Color.FromArgb("#1F2328")
                },
                HeightRequest = 420
            };
            container.Add(scroll);
            Grid.SetRow(scroll, 1);

            var closeButton = new Button
            {
                Text = accept,
                BackgroundColor = Color.FromArgb("#2DA44E"),
                TextColor = Colors.White,
                CornerRadius = 12,
                Padding = new Thickness(16, 10)
            };
            closeButton.Clicked += (_, __) => popup.Close();
            container.Add(closeButton);
            Grid.SetRow(closeButton, 2);

            popup.Content = new Border
            {
                Stroke = Color.FromArgb("#D0D7DE"),
                StrokeThickness = 1,
                Background = Colors.White,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Content = container
            };

            await ShowPopupAsync(popup);
        }
        catch
        {
            // Fallback to DisplayAlert in case Popup cannot be shown on some platforms/themes
            await AlertAsync(title, text, accept);
        }
    }

    // Display error dialog with copy button (cross-platform)
    public async Task ShowErrorAsync(string errorMessage, string stackTrace)
    {
        var fullError = $"错误信息：\n{errorMessage}\n\n堆栈跟踪：\n{stackTrace}";

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                var popup = new CommunityToolkit.Maui.Views.Popup
                {
                    CanBeDismissedByTappingOutsideOfPopup = false
                };

                var container = new Grid
                {
                    Padding = 16,
                    BackgroundColor = Colors.White,
                    RowSpacing = 12,
                    RowDefinitions = new RowDefinitionCollection
                    {
                        new RowDefinition(GridLength.Auto),   // Title
                        new RowDefinition(GridLength.Star),   // ScrollView
                        new RowDefinition(GridLength.Auto)    // Buttons
                    }
                };

                // Title with error icon
                var titleStack = new HorizontalStackLayout
                {
                    Spacing = 8
                };
                titleStack.Add(new Label
                {
                    Text = "⚠️",
                    FontSize = 24,
                    VerticalOptions = LayoutOptions.Center
                });
                titleStack.Add(new Label
                {
                    Text = "应用程序错误",
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 18,
                    TextColor = Color.FromArgb("#CF222E"),
                    VerticalOptions = LayoutOptions.Center
                });
                container.Add(titleStack);
                Grid.SetRow(titleStack, 0);

                // Scrollable error content
                var scroll = new ScrollView
                {
                    HeightRequest = DeviceInfo.Platform == DevicePlatform.Android ? 300 : 400,
                    Content = new Label
                    {
                        Text = fullError,
                        FontFamily = "monospace",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#1F2328"),
                        LineBreakMode = LineBreakMode.WordWrap
                    }
                };
                container.Add(scroll);
                Grid.SetRow(scroll, 1);

                // Button container
                var buttonGrid = new Grid
                {
                    ColumnSpacing = 8,
                    ColumnDefinitions = new ColumnDefinitionCollection
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Star)
                    }
                };

                // Copy button
                var copyButton = new Button
                {
                    Text = "📋 复制错误信息",
                    BackgroundColor = Color.FromArgb("#0969DA"),
                    TextColor = Colors.White,
                    CornerRadius = 12,
                    Padding = new Thickness(16, 12),
                    FontSize = 14
                };
                copyButton.Clicked += async (_, __) =>
                {
                    try
                    {
                        await Clipboard.SetTextAsync(fullError);
                        copyButton.Text = "✅ 已复制";
                        await Task.Delay(2000);
                        copyButton.Text = "📋 复制错误信息";
                    }
                    catch { }
                };
                buttonGrid.Add(copyButton);
                Grid.SetColumn(copyButton, 0);

                // Close button
                var closeButton = new Button
                {
                    Text = "关闭",
                    BackgroundColor = Color.FromArgb("#CF222E"),
                    TextColor = Colors.White,
                    CornerRadius = 12,
                    Padding = new Thickness(16, 12),
                    FontSize = 14
                };
                closeButton.Clicked += (_, __) => popup.Close();
                buttonGrid.Add(closeButton);
                Grid.SetColumn(closeButton, 1);

                container.Add(buttonGrid);
                Grid.SetRow(buttonGrid, 2);

                popup.Content = new Border
                {
                    Stroke = Color.FromArgb("#CF222E"),
                    StrokeThickness = 2,
                    Background = Colors.White,
                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                    Content = container,
                    WidthRequest = DeviceInfo.Platform == DevicePlatform.Android ? 350 : 500
                };

                await ShowPopupAsync(popup);
            }
            catch
            {
                // Fallback to simple alert if popup fails
                var page = CurrentPage;
                if (page != null)
                {
                    var result = await page.DisplayAlertAsync(
                        "⚠️ 应用程序错误",
                        $"{errorMessage}\n\n点击【确定】复制错误信息",
                        "确定",
                        "取消");

                    if (result)
                    {
                        await Clipboard.SetTextAsync(fullError);
                    }
                }
            }
        });
    }
}
