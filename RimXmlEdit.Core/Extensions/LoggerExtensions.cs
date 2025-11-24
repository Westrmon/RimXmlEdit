using Microsoft.Extensions.Logging;
using RimXmlEdit.Core.Utils;
using System.Runtime.CompilerServices;

namespace RimXmlEdit.Core.Extensions;

internal static class LoggerExtensions
{
    /// <summary>
    /// 获取一个以调用者类型全名为名称的 ILogger 实例。
    /// </summary>
    /// <param name="obj"> 任意对象（通常是 this） </param>
    /// <param name="name"> 可选的自定义名称，默认为调用者类型全名 </param>
    /// <returns> ILogger </returns>
    public static ILogger Log(this object obj, [CallerMemberName] string? name = null)
    {
        // 如果显式传了 name，就用它；否则使用调用者的类型名
        var loggerName = obj.GetType().FullName ?? name ?? "Unknown";
        return LoggerFactoryInstance.Factory.CreateLogger(loggerName);
    }

    public static void LogNotify(this ILogger logger, string message, params object[] args)
    {
        logger.LogInformation(LoggerFactoryInstance.NotifyEventId, message, args);
    }

    // 也可以重载一个带标题/样式的，或者其他级别的
    public static void LogNotifySuccess(this ILogger logger, string message, params object[] args)
    {
        // 依然是 Info 级别，但带有强制弹窗 ID
        logger.LogInformation(LoggerFactoryInstance.NotifyEventId, message, args);
    }
}
