using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimXmlEdit.Core.NodeGeneration;

internal interface INodeUpdateRule : IRuleBase
{
    /// <summary>
    /// 更新节点结构
    /// </summary>
    /// <param name="parentTgName"> 父节点标签名 </param>
    /// <param name="tgName"> 当前节点标签名 </param>
    /// <param name="value"> 参数 </param>
    /// <returns> </returns>
    public NodeBlueprint UpdateNode(NodeBlueprint defaultRootNode, string parentTgName, string tgName, string? value);
}
