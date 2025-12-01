using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using RimXmlEdit.Core.Extensions;

namespace RimXmlEdit.Core.Trans;

/// <summary>
///     流式保存服务
///     支持随时接收新的翻译Token，并在后台自动分批写入文件
/// </summary>
public class TransWriter : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, byte> _dirtyFiles = new();
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _fileBuffer = new();
    private readonly Channel<TransToken> _inputChannel;
    private readonly ILogger _log;
    private Task? _saveTask;

    public TransWriter(string targetLanguage)
    {
        TargetLanguage = targetLanguage;
        _log = this.Log();
        _inputChannel = Channel.CreateUnbounded<TransToken>();
    }

    public string TargetLanguage { get; }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    /// <summary>
    ///     启动后台保存服务
    /// </summary>
    public void Start()
    {
        if (_saveTask != null) return;
        _saveTask = Task.Run(SaveLoopAsync);
    }

    /// <summary>
    ///     提交一个已翻译的 Token (非阻塞)
    /// </summary>
    /// <param name="token">包含翻译后文本的 Token</param>
    public void Enqueue(TransToken token)
    {
        if (!_inputChannel.Writer.TryWrite(token)) _log?.LogWarning("Failed to enqueue token: {TokenKey}", token.Key);
    }

    /// <summary>
    ///     停止服务并强制保存剩余数据
    /// </summary>
    public async Task StopAsync()
    {
        _inputChannel.Writer.TryComplete();
        if (_saveTask != null) await _saveTask;
    }

    /// <summary>
    ///     后台主循环
    /// </summary>
    private async Task SaveLoopAsync()
    {
        try
        {
            while (await _inputChannel.Reader.WaitToReadAsync() || !_dirtyFiles.IsEmpty)
            {
                while (_inputChannel.Reader.TryRead(out var token)) UpdateBuffer(token);
                if (!_dirtyFiles.IsEmpty) await FlushDirtyFilesAsync();
                await Task.Delay(500);
            }
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "TransWriter background loop crashed.");
        }
    }

    private void UpdateBuffer(TransToken token)
    {
        var targetPath = CalculateTargetPath(token);
        if (string.IsNullOrEmpty(targetPath)) return;
        var fileData = _fileBuffer.GetOrAdd(targetPath, path =>
        {
            // === 新增逻辑：初始化时尝试从磁盘读取现有内容 ===
            return LoadDictFromDisk(path);
        });
        lock (fileData)
        {
            fileData[token.Key] = token.OriginalValue;
        }

        _dirtyFiles.TryAdd(targetPath, 1);
    }

    private async Task FlushDirtyFilesAsync()
    {
        var filesToSave = _dirtyFiles.Keys.ToList();

        foreach (var filePath in filesToSave)
        {
            _dirtyFiles.TryRemove(filePath, out _);

            if (!_fileBuffer.TryGetValue(filePath, out var data)) continue;
            Dictionary<string, string> snapshot;
            lock (data)
            {
                snapshot = new Dictionary<string, string>(data);
            }

            await WriteXmlAsync(filePath, snapshot);
        }
    }

    private async Task WriteXmlAsync(string path, Dictionary<string, string> data)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"));
            var root = new XElement("LanguageData");
            doc.Add(root);

            foreach (var kvp in data.OrderBy(k => k.Key)) root.Add(new XElement(kvp.Key, kvp.Value));

            var tempPath = path + ".tmp";

            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                await doc.SaveAsync(writer, SaveOptions.None, _cts.Token);
            }

            if (File.Exists(path)) File.Delete(path);
            File.Move(tempPath, path);

            _log?.LogInformation("Saved: {GetFileName} ({DataCount} keys)", Path.GetFileName(path), data.Count);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, $"Failed to save file: {path}");
            _dirtyFiles.TryAdd(path, 1);
        }
    }

    private string CalculateTargetPath(TransToken token)
    {
        var sourceFile = token.SourceFile;
        var fileName = Path.GetFileName(sourceFile);

        if (token.Type == TransNodeType.Key)
            return Path.Combine(token.RootFile, "Languages", TargetLanguage, "Keyed", fileName);

        var defsIndex = sourceFile.IndexOf("Defs", StringComparison.OrdinalIgnoreCase);
        if (defsIndex == -1)
            return Path.Combine(token.RootFile, "Languages", TargetLanguage, "DefInjected", "Misc", fileName);

        var relativePath = sourceFile[(defsIndex + 4)..]
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.Combine(token.RootFile, "Languages", TargetLanguage, "DefInjected", relativePath);
    }

    private Dictionary<string, string> LoadDictFromDisk(string path)
    {
        var dict = new Dictionary<string, string>();
        if (!File.Exists(path)) return dict;

        try
        {
            var doc = XDocument.Load(path);
            if (doc.Root != null)
                foreach (var el in doc.Root.Elements())
                    dict[el.Name.LocalName] = el.Value;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, $"Error pre-loading file: {path}");
        }

        return dict;
    }
}