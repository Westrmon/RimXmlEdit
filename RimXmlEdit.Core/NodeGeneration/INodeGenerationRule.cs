namespace RimXmlEdit.Core.NodeGeneration;

internal interface INodeGenerationRule : IRuleBase
{
    /// <summary>
    /// 生成节点蓝图
    /// </summary>
    NodeBlueprint CreateBlueprint(string selectedItem);
}
