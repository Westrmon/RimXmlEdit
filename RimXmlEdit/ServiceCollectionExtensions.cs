using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RimXmlEdit.Core;
using RimXmlEdit.Core.NodeGeneration;
using RimXmlEdit.Core.Parse;
using RimXmlEdit.Core.Utils;
using RimXmlEdit.Utils;
using RimXmlEdit.ViewModels;
using RimXmlEdit.Views;

namespace RimXmlEdit;

public static class ServiceCollectionExtensions
{
    public static void AddCommonServices(
        this IServiceCollection collection,
        IConfiguration configuration)
    {
        collection.Configure<AppSettings>(configuration.GetSection("AppSettings"));

        collection.AddSingleton<MainWindow>();
        collection.AddTransient<InitWindow>();
        collection.AddTransient<SettingsView>();
        collection.AddTransient<AboutWindow>();

        collection.AddSingleton<ILocalizationService, LocalizationService>();
        collection.AddSingleton<SimpleFileExplorerViewModel>();
        collection.AddSingleton<ISimpleFileExplorer, SimpleFileExplorerViewModel>(f =>
        {
            return f.GetRequiredService<SimpleFileExplorerViewModel>();
        });
        collection.AddSingleton<QuickSearchBoxViewModel>();
        collection.AddSingleton<IQuickSearch, QuickSearchBoxViewModel>(f =>
        {
            return f.GetRequiredService<QuickSearchBoxViewModel>();
        });
        collection.AddTransient<MainViewModel>();
        collection.AddTransient<InitViewModel>();
        collection.AddSingleton<SidebarViewModel>();
        collection.AddTransient<RecentProjectsViewModel>();
        collection.AddTransient<CreateNewProjectViewModel>();
        collection.AddTransient<GetTextViewModel>();
        collection.AddTransient<SettingsViewModel>();
        collection.AddTransient<AboutViewModel>();

        collection.AddSingleton<TempConfig>();
        collection.AddSingleton<ModParser>();
        collection.AddSingleton<NodeInfoManager>();
        collection.AddSingleton<NodeGenerationService>();
    }
}
