using MessagePack;
using MessagePack.Formatters;
using RimXmlEdit.Core.Entries;

namespace RimXmlEdit.Core.Utils;

public class ObjectFieldInfoFormatter : IMessagePackFormatter<object>
{
    public object Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil()) return null!;

        var nextType = reader.NextMessagePackType;
        var resolver = options.Resolver;

        switch (nextType)
        {
            case MessagePackType.String:
                return reader.ReadString();

            case MessagePackType.Array:
                var peekReader = reader;
                var arrayCount = peekReader.ReadArrayHeader();

                if (arrayCount > 0)
                {
                    if (peekReader.NextMessagePackType == MessagePackType.String)
                    {
                        var firstElement = peekReader.ReadString();
                        if (firstElement == "DefCache")
                        {
                            // 确认为 DefCache，使用原始 reader 进行反序列化
                            return MessagePackSerializer.Deserialize<DefCache>(ref reader, options);
                        }
                    }
                }
                return MessagePackSerializer.Deserialize<List<XmlFieldInfo>>(ref reader, options);

            case MessagePackType.Map:
                return MessagePackSerializer.Deserialize<Dictionary<string, XmlFieldInfo>>(ref reader, options);

            default:
                throw new MessagePackSerializationException($"Unsupported MessagePackType: {nextType}");
        }
    }

    public void Serialize(ref MessagePackWriter writer, object value, MessagePackSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNil();
            return;
        }

        var resolver = options.Resolver;

        switch (value)
        {
            case string s:
                writer.Write(s);
                break;

            case List<XmlFieldInfo> list:
                MessagePackSerializer.Serialize(ref writer, list, options);
                break;

            case Dictionary<string, XmlFieldInfo> dict:
                MessagePackSerializer.Serialize(ref writer, dict, options);
                break;

            default:
                throw new MessagePackSerializationException($"Unsupported object type: {value.GetType()}");
        }
    }
}
