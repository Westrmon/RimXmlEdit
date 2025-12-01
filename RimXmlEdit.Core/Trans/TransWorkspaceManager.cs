using System.Text.Json;

namespace RimXmlEdit.Core.Trans;

public class TransWorkspaceManager
{
    private readonly string _modRootPath;

    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        TypeInfoResolver = TransJsonSerializerContext.Default
    };

    private readonly TransNode _transNode;

    public TransWorkspaceManager(string modRootPath, TransNode transNode)
    {
        _modRootPath = modRootPath;
        _transNode = transNode;
    }


    public async Task ExportWorkspaceAsync(IEnumerable<TransToken> tokens, string savePath)
    {
        var units = tokens.Select(token => new TranslationUnit
            {
                Key = token.Key,
                Original = token.OriginalValue,
                Translation = "",
                RelativePath = token.SourceFile,
                Type = token.Type
            })
            .ToList();

        using var stream = File.Create(savePath);
        await JsonSerializer.SerializeAsync(stream, units, _options);
    }

    public async Task CreateMergedWorkspaceAsync(
        IEnumerable<TransToken> tokens,
        string targetLangPath,
        string savePath,
        bool loadExistTranslations = false)
    {
        var existingData = _transNode.LoadExistingLanguageData(targetLangPath);
        var units = new List<TranslationUnit>();

        foreach (var token in tokens)
        {
            var currentTranslation = "";
            if (existingData.TryGetValue(token.Key, out var existingText))
            {
                if (loadExistTranslations)
                    currentTranslation = existingText;
                else
                    continue;
            }

            units.Add(new TranslationUnit
            {
                Key = token.Key,
                Original = token.OriginalValue,
                Translation = currentTranslation,
                RelativePath = token.SourceFile,
                Type = token.Type
            });
        }

        if (units.Count == 0) return;
        using var stream = File.Create(savePath);
        await JsonSerializer.SerializeAsync(stream, units, _options);
    }


    public async Task ApplyWorkspaceAsync(
        string workspaceFilePath,
        TransWriter writer,
        bool saveEmptyTranslations = false)
    {
        if (!File.Exists(workspaceFilePath)) throw new FileNotFoundException("Workspace file not found");

        List<TranslationUnit>? units;
        using (var stream = File.OpenRead(workspaceFilePath))
        {
            units = await JsonSerializer.DeserializeAsync<List<TranslationUnit>>(stream, _options);
        }

        if (units == null) return;

        for (var index = 0; index < units.Count; index++)
        {
            var unit = units[index];
            if (!saveEmptyTranslations && string.IsNullOrWhiteSpace(unit.Translation)) continue;

            var absoluteSourcePath = Path.GetFullPath(Path.Combine(_modRootPath, unit.RelativePath));
            var token = new TransToken
            {
                Key = unit.Key,
                OriginalValue = unit.Translation,
                Type = unit.Type,
                RootFile = _modRootPath,
                SourceFile = unit.RelativePath,
                IsList = false
            };
            writer.Enqueue(token);
        }
    }
}