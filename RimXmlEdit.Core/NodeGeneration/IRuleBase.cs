using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimXmlEdit.Core.NodeGeneration;

internal interface IRuleBase
{
    /// <summary>
    /// 判断当前规则是否适用于上下文
    /// </summary>
    /// <param name="parentTagName"> 父节点标签名 </param>
    /// <param name="selectedItem"> 要插入的节点文本 </param>
    /// <param name="Identification"> 节点标识, 进一步匹配规则 </param>
    bool CanApply(bool isPatch, string parentTagName, string selectedItem, string identification);
}
