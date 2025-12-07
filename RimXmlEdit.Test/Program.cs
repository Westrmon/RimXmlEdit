using System.Diagnostics;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using RimXmlEdit.Core;
using RimXmlEdit.Core.AI;
using RimXmlEdit.Core.Entries;
using RimXmlEdit.Core.Extensions;
using RimXmlEdit.Core.NodeDefine;
using RimXmlEdit.Core.NodeGeneration;
using RimXmlEdit.Core.Parse;
using RimXmlEdit.Core.Trans;
using RimXmlEdit.Core.Utils;

namespace RimXmlEdit.Test;

public class Program
{
    public static void Main(string[] args)
    {
        var vs = @"D:\steam\steamapps\common";
        TempConfig.GamePath = @"D:\steam\steamapps\common\RimWorld";
        MessagePackSerializer.DefaultOptions = MessagePackSerializerOptions.Standard
            .WithResolver(CompositeResolver.Create(
                new IMessagePackFormatter[] { new ObjectFieldInfoFormatter() },
                new IFormatterResolver[] { StandardResolver.Instance }));
        // .WithCompression(MessagePackCompression.Lz4Block);
        Test3();
    }

    private static void Test1()
    {
        var s = new DefParser();
        s.Init();
        var process = Process.GetCurrentProcess();
        Console.WriteLine($"物理内存占用: {process.WorkingSet64 / 1024 / 1024} MB");
        Console.WriteLine($"私有内存占用: {process.PrivateMemorySize64 / 1024 / 1024} MB");
        var ds2 = s.Parse(true);
        var process2 = Process.GetCurrentProcess();
        Console.WriteLine($"物理内存占用: {process2.WorkingSet64 / 1024 / 1024} MB");
        Console.WriteLine($"私有内存占用: {process2.PrivateMemorySize64 / 1024 / 1024} MB");
        var graphicData = ds2.DefInfos.First(t => t.TagName == "ThingDef").Fields.First(t => t.Name == "graphicData");
        var sd = graphicData.ResolveChildren(ds2.Schemas);
    }

    public static void Test1_1()
    {
        var s = new DefParser();
        s.Init();
        s.Init(@"D:\steam\steamapps\workshop\content\294100\839005762\1.6\Assemblies\AlienRace.dll");
        s.Init(@"D:\steam\steamapps\workshop\content\294100\3394862242\1.6\Source\bin\Debug\NewRatkin.dll");
        s.Init(@"D:\Desktop\3293914637\1.6\Assemblies\RatkinAnomaly.dll");
        s.Parse();
    }

    private static void Test2()
    {
        var st1 = @"D:\steam\steamapps\workshop\content\294100\2816826107\1.4\Defs\Hediff_DrugV.xml";
        var st = @"D:\steam\steamapps\workshop\content\294100\2478833213\1.5\Patches\Combat_Extanded.xml";
        var st3 = @"D:\Desktop\3211734754\About\About.xml";
        var st2 = @"D:\Desktop\Hediffs_EnemyImplants.xml";
        var txt = File.ReadAllText(st3);
        var de = XmlConverter.Deserialize(txt);
        var txt2 = XmlConverter.SerializeAbout(de);
        File.WriteAllText(@"D:\Desktop\test.xml", txt2);
    }

    private static void Test3()
    {
        var appsetting = new AppSettings();
        using var ft = File.OpenText("AiTest.txt");
        var content = ft.ReadToEnd().Split('\n').Select(t => t.Split('=').Last().Trim()).ToArray();
        appsetting.AI.Endpoint = content[0];
        appsetting.AI.ModelId = content[1];
        appsetting.AI.ApiKey = content[2];
        var prompt = """
                     给定一系列环世界（RimWorld）的XML标签，请解释每个节点在游戏中的作用。返回纯JSON格式，其中键为节点名称（字符串），值为对该节点作用的准确简练描述（字符串, 30字内）。描述需基于游戏机制，贴合游戏事实，避免直接翻译节点名称。
                     要求：
                     描述应简洁明了，聚焦于节点在游戏中的实际功能。
                     确保描述符合环世界游戏的设定和规则，不引入主观猜测。
                     节点名称保持原样作为JSON的键，无需修改或翻译。
                     示例输入
                     - healthScale
                     示例输出：
                     {
                     "healthScale": "控制生物体的生命值缩放因子，影响其最大健康值。",
                     }
                     请严格按照此格式响应，只输出JSON对象，不附加任何额外文本,不要包含 Markdown 代码块标记。待处理列表：
                     """;
        // var sets = "你是一个只输出 JSON 的 API 接口。";
        appsetting.AI.AiPromptForDefineString = prompt;
        appsetting.AI.AIProvider = AiProvider.OpenAI;
        NodeInfoManager manager = new(null);
        manager.Init();
        var defineInfo = new NodeDefineInfo(appsetting, manager.DataCache);
        defineInfo.MaxDepth = 4;
        defineInfo.Init();
        // var s = new RXStruct();
        // manager.DataCache.Comps.Where(t => t.IsThingComp).ToList()
        //     .ForEach(g =>
        //     {
        //         s.Defs.Add(new DefInfo() { TagName = g.FullName, Fields = g.Fields });
        //     });
        LoggerFactoryInstance.SetConsoleLevel(LoggerFactoryInstance.LogLevelConfig.Debug);
        // var mod = new ModParser();
        // var s = mod.TryParse([@"D:\steam\steamapps\workshop\content\294100"], ModParser.ParseRange.CommunityMod);
        // var ds = s.SelectMany(t => t.Defs);
        // defineInfo.CreateModDefineFile(ds, "common");
        // defineInfo.AutoFillDescriptionsWithAi(batches: 150).Wait();

        Console.WriteLine("Finished");
        while (true)
        {
        }
    }

    private static void Test4()
    {
        var parse = new ModParser();
        var infos = parse.Parse();
        Console.WriteLine(infos.Count());
    }

    // 比较dll标签和实际所用标签的差异
    private static void Test5()
    {
        var modInfo = new ModParser().Parse(
            ["D:\\steam\\steamapps\\common\\RimWorld\\Mods", "D:\\steam\\steamapps\\workshop\\content\\294100"],
            ModParser.ParseRange.Core | ModParser.ParseRange.DLC);
        var parse = new DefParser();
        parse.Init();
        var defInfo = parse.Parse();
        parse.Init("");
        LoggerFactoryInstance.SetLevels(LoggerFactoryInstance.LogLevelConfig.Error,
            LoggerFactoryInstance.LogLevelConfig.Error);
        List<DefInfo> used = new();

        var ds = modInfo.SelectMany(m => m.Defs)
            .SelectMany(d => d.Defs)
            .GroupBy(t => t.TagName.Split('_')[0])
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderBy(c => c.Count);
        foreach (var i in ds) Console.WriteLine(i.Name + " => " + i.Count);
        var names = ds.Select(t => t.Name);
        var sdf = defInfo.DefInfos.Where(t => !names.Contains(t.TagName));
        foreach (var i in sdf)
            if (i.TagName.EndsWith("Def"))
                Console.WriteLine(i.TagName);
    }

    private static void Test5_1()
    {
        var dllPath = @"D:\Desktop\839005762\1.6\Assemblies\AlienRace.dll";
        var parse = new NodeInfoManager(null);
        parse.Init();
        parse.AddDll(dllPath);
        var data = parse.DataCache;
        var race = data.DefInfos.First(f => f.TagName == "ThingDef_AlienRace");
        var s = race.Fields.First(f => f.Name == "alienRace").ResolveChildren(data.Schemas);
        Console.WriteLine(s.Count);
    }

    private static void Test6()
    {
        NodeInfoManager manager = new(null);
        manager.Init();
        manager.AddDll(@"D:\steam\steamapps\workshop\content\294100\839005762\1.6\Assemblies\AlienRace.dll");
        manager.AddDll(@"D:\steam\steamapps\workshop\content\294100\3032870787\1.6\Assemblies\WarCrimesExpanded2.dll");
        // var s = manager.GetNodeByDefName("VoidMonolith");
        var parser = new ModParser();
        var modRootPath = @"D:\Desktop\2844129100";
        var langPath = @"D:\steam\steamapps\workshop\content\294100\2847942445\1.6";
        // string modRootPath = @"D:\Desktop\New folder";
        var nodes = new TransNode(parser, manager);
        nodes.TransInit(modRootPath, "ChineseSimplified");
        var trans = nodes.GetTransToken();
        // nodes.TransInit(mod2, "ChineseSimplified");
        // trans.AddRange(nodes.GetTransToken().ToList());
        var ext = nodes.LoadExistingLanguageData(Path.Combine(langPath, "Languages", "ChineseSimplified"));
        Debug.Assert(trans.Count() != 0);
        // CheckDiff(trans, ext);
        nodes.ExportWorkspaceAsync(trans, @"D:\Desktop\trans.json").GetAwaiter().GetResult();
        //nodes.ApplyWorkspaceAsync(@"D:\Desktop\trans.json", true).GetAwaiter().GetResult();
        while (true)
        {
        }
    }

    private static void Test7()
    {
        var manager = new NodeInfoManager(null);
        manager.Init();
        var s = manager.GetChildList("ThingDef");
        Console.WriteLine(s.Count());
        var s2 = manager.GetChildList("ThingDef/containedItemsSelectable");
        manager.GetChildList("TestNode/TestNode2");
        manager.GetChildList("AnimationDef/loopMode");
    }

    private static void Test8()
    {
        var manager = new NodeInfoManager(null);
        manager.Init();
        var a1 = "TestNode/TestNode2";
        var va1 = "55";
        Console.WriteLine(a1 + " => " + manager.CheckValueIsValid(a1, va1));
        var a2 = "AnimationDef/loopMode";
        var va2 = "Clamp";
        Console.WriteLine(a2 + " => " + manager.CheckValueIsValid(a2, va2));
        var a3 = "ApparelProperties/careIfDamaged";
        var va3 = "55";
        Console.WriteLine(a3 + " => " + manager.CheckValueIsValid(a3, va3));
        Console.WriteLine(a3 + " => " + manager.CheckValueIsValid(a3, "true"));
    }

    private static void Test9()
    {
        var st1 = @"D:\Code\Programs\RimXmlEdit\RimXmlEdit.Desktop\bin\Debug\net9.0\Projects\MyAwesomeMod";
        var st2 = @"D:\steam\steamapps\common\RimWorld\Mods";
        var results = FileStrSearch.Search(st1, "name", new FileStrSearch.SearchOptions
        {
            CaseSensitive = false,
            FileExtensions = ["xml"],
            UseParallelProcessing = true,
            MatchType = SearchType.WholeWord
        });
        foreach (var item in results) Console.WriteLine(item);
    }

    private static void Test10()
    {
        var t = new NodeGenerationService(new FileFromRule(new ExampleXmlManager()));
        var node = t.Generate(false, "Defs", "ThingDef", "ThingDef");
    }

    private static void Test11()
    {
        var manager = new ExampleXmlManager();
        manager.Init();
        var list = manager.GetExampleXmlList();
        var types = manager.GetAllTemplateType();
        var fi = manager.GetFilterInfos(list.First().Key);
        var blue = manager.CreateBlueprint(list.First().Key);
    }

    private static void Test12()
    {
    }

    private static void CheckDiff(IEnumerable<TransToken> tokens, Dictionary<string, string> ex)
    {
        var item1 = tokens.Select(t => t.Key).ToHashSet();
        var item2 = ex.Keys.Select(k => k).ToHashSet();
        foreach (var item in ex)
            if (item.Value == "TODO")
                item2.Remove(item.Key);

        foreach (var item in item1.ToList())
            if (item2.Remove(item))
                item1.Remove(item);

        var temp = new Dictionary<string, string>();
        foreach (var item in item2) temp.Add(item, ex[item]);
        Console.WriteLine(item1.Count);
        Console.WriteLine(temp.Count);
    }
}