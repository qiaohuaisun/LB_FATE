namespace LB_FATE.Mobile.Models;

/// <summary>
/// 日志消息模型 - 支持颜色着色
/// </summary>
public class LogMessage
{
    public string Text { get; set; } = "";
    public Color TextColor { get; set; } = Color.FromArgb("#1F2328");
    public bool IsBold { get; set; } = false;
    public string Time { get; set; } = "";

    public static LogMessage Create(string text)
    {
        var msg = new LogMessage
        {
            Text = text,
            Time = DateTime.Now.ToString("HH:mm:ss")
        };

        // 分析消息类型并设置颜色
        var lowerText = text.ToLowerInvariant();

        // Boss行动 - 红色加粗
        if (text.Contains("【") && text.Contains("】"))
        {
            msg.TextColor = Color.FromArgb("#CF222E");
            msg.IsBold = true;
        }
        // 伤害消息 - 红色
        else if (lowerText.Contains("伤害") || lowerText.Contains("damage") ||
                 lowerText.Contains("受到") || text.Contains("💥"))
        {
            msg.TextColor = Color.FromArgb("#CF222E");
        }
        // 治疗消息 - 绿色
        else if (lowerText.Contains("治疗") || lowerText.Contains("heal") ||
                 lowerText.Contains("回复") || lowerText.Contains("恢复"))
        {
            msg.TextColor = Color.FromArgb("#1A7F37");
        }
        // 技能使用 - 紫色
        else if (lowerText.Contains("使用") || lowerText.Contains("use") ||
                 lowerText.Contains("释放") || text.Contains("⚡"))
        {
            msg.TextColor = Color.FromArgb("#8250DF");
        }
        // 移动 - 蓝色
        else if (lowerText.Contains("移动") || lowerText.Contains("move") || text.Contains("🚶"))
        {
            msg.TextColor = Color.FromArgb("#0969DA");
        }
        // 攻击 - 橙色
        else if (lowerText.Contains("攻击") || lowerText.Contains("attack") || text.Contains("⚔️"))
        {
            msg.TextColor = Color.FromArgb("#FB8500");
        }
        // 警告/错误 - 金色
        else if (lowerText.Contains("警告") || lowerText.Contains("warning") ||
                 lowerText.Contains("错误") || lowerText.Contains("error") || text.Contains("⚠️"))
        {
            msg.TextColor = Color.FromArgb("#BF8700");
            msg.IsBold = true;
        }
        // 系统消息 - 灰色
        else if (text.StartsWith("===") || text.StartsWith("---") ||
                 lowerText.Contains("已连接") || lowerText.Contains("等待"))
        {
            msg.TextColor = Color.FromArgb("#656D76");
        }

        return msg;
    }
}
