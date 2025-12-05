using System.Text;
using Microsoft.Extensions.AI;

namespace RimXmlEdit.Core.AI;

public class AiAssistant
{
    private readonly IChatClient _chatClient;
    private readonly List<ChatMessage> _history;

    public AiAssistant(IChatClient chatClient)
    {
        _chatClient = chatClient;
        _history = new List<ChatMessage>();
    }

    /// <summary>
    ///     设置系统提示词
    /// </summary>
    public void SetSystemPrompt(string prompt)
    {
        // 清除之前的 System 消息（如果存在）并添加新的
        _history.RemoveAll(m => m.Role == ChatRole.System);
        _history.Insert(0, new ChatMessage(ChatRole.System, prompt));
    }

    /// <summary>
    ///     发送消息并获取回复 (非流式)
    /// </summary>
    public async Task<string> AskAsync(string userMessage)
    {
        _history.Add(new ChatMessage(ChatRole.User, userMessage));
        var response = await _chatClient.GetResponseAsync(_history);
        return response.Text ?? string.Empty;
    }

    /// <summary>
    ///     发送消息并获取流式回复
    /// </summary>
    public async IAsyncEnumerable<string> AskStreamAsync(string userMessage)
    {
        _history.Add(new ChatMessage(ChatRole.User, userMessage));

        var responseBuilder = new StringBuilder();

        await foreach (var update in _chatClient.GetStreamingResponseAsync(_history))
            if (!string.IsNullOrEmpty(update.Text))
            {
                responseBuilder.Append(update.Text);
                yield return update.Text;
            }

        _history.Add(new ChatMessage(ChatRole.Assistant, responseBuilder.ToString()));
    }

    /// <summary>
    ///     清空对话历史
    /// </summary>
    public void ClearHistory()
    {
        _history.Clear();
    }
}