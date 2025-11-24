using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace RimXmlEdit.Core.Utils;

/// <summary>
/// 定义搜索的匹配模式
/// </summary>
public enum SearchType
{
    /// <summary>
    /// 包含匹配 (子字符串)
    /// </summary>
    Contains,

    /// <summary>
    /// 全词匹配
    /// </summary>
    WholeWord,

    /// <summary>
    /// 正则表达式匹配
    /// </summary>
    Regex
}

public class FileStrSearch
{
    public class SearchOptions
    {
        public bool CaseSensitive { get; set; } = false;
        public string[] FileExtensions { get; set; } = { "*" };
        public bool UseParallelProcessing { get; set; } = true;
        public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// 设置搜索的匹配模式 (包含/全词/正则)
        /// </summary>
        public SearchType MatchType { get; set; } = SearchType.Contains;
    }

    public static List<SearchResult> Search(
        string rootPath,
        string searchText,
        SearchOptions options = null)
    {
        options ??= new SearchOptions();
        var results = new List<SearchResult>();

        // 获取文件枚举
        var files = GetFiles(rootPath, options.FileExtensions);

        // 并行或顺序处理
        if (options.UseParallelProcessing)
        {
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = options.MaxDegreeOfParallelism
            };

            var concurrentResults = new ConcurrentBag<SearchResult>();

            Parallel.ForEach(files, parallelOptions, file =>
            {
                var result = SearchInFile(file, searchText, options);
                if (result != null)
                    concurrentResults.Add(result);
            });

            results = concurrentResults.ToList();
        }
        else
        {
            foreach (var file in files)
            {
                var result = SearchInFile(file, searchText, options);
                if (result != null)
                    results.Add(result);
            }
        }

        return results;
    }

    private static IEnumerable<string> GetFiles(string path, string[] extensions)
    {
        if (extensions.Length == 1 && extensions[0] == "*")
        {
            return Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories);
        }
        var extensionSet = new HashSet<string>(
            extensions.Select(ext => "." + ext.TrimStart('.').ToLowerInvariant())
        );
        return Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                        .Where(file => extensionSet.Contains(Path.GetExtension(file).ToLowerInvariant()));
    }

    private static SearchResult SearchInFile(string filePath, string searchText, SearchOptions options)
    {
        try
        {
            var matches = new List<LineMatch>();
            Func<string, bool> isMatch;

            // 根据搜索选项创建匹配逻辑
            switch (options.MatchType)
            {
                case SearchType.Regex:
                    try
                    {
                        var regexOptions = RegexOptions.None;
                        if (!options.CaseSensitive)
                            regexOptions |= RegexOptions.IgnoreCase;
                        var regex = new Regex(searchText, regexOptions);
                        isMatch = line => regex.IsMatch(line);
                    }
                    catch (ArgumentException ex)
                    {
                        // 处理无效的正则表达式
                        return new SearchResult { FilePath = filePath, ErrorMessage = "无效的正则表达式: " + ex.Message };
                    }
                    break;

                case SearchType.WholeWord:
                    try
                    {
                        var pattern = $@"\b{Regex.Escape(searchText)}\b";
                        var regexOptions = RegexOptions.None;
                        if (!options.CaseSensitive)
                            regexOptions |= RegexOptions.IgnoreCase;
                        var regex = new Regex(pattern, regexOptions);
                        isMatch = line => regex.IsMatch(line);
                    }
                    catch (ArgumentException ex)
                    {
                        // Regex.Escape 应该可以防止这种情况，但作为安全措施保留
                        return new SearchResult { FilePath = filePath, ErrorMessage = "创建全词匹配模式时出错: " + ex.Message };
                    }
                    break;

                default: // 默认是 Contains 模式
                    var comparison = options.CaseSensitive ?
                        StringComparison.Ordinal :
                        StringComparison.OrdinalIgnoreCase;
                    isMatch = line => line.Contains(searchText, comparison);
                    break;
            }

            using var reader = new StreamReader(filePath);
            string line;
            int lineNumber = 0;

            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (isMatch(line))
                {
                    matches.Add(new LineMatch
                    {
                        LineNumber = lineNumber,
                        Content = line.Trim()
                    });
                }
            }

            return matches.Any() ?
                new SearchResult { FilePath = filePath, Matches = matches } :
                null;
        }
        catch (Exception ex)
        {
            return new SearchResult { FilePath = filePath, ErrorMessage = ex.Message };
        }
    }
}

public class SearchResult
{
    public string FilePath { get; set; }
    public List<LineMatch> Matches { get; set; }

    public string ErrorMessage { get; set; }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public override string ToString()
    {
        return $"[{(HasError ? "Error" : "Match")}] {FilePath} {string.Join(';', Matches.Select(e => e.LineNumber))}";
    }
}

public class LineMatch
{
    public int LineNumber { get; set; }
    public string Content { get; set; }
}
