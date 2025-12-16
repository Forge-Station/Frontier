using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;


namespace Content.Shared._NC.Trade;


[Serializable, NetSerializable,]
public readonly record struct IntRange(int Min, int Max)
{
    public static IntRange Fixed(int value) => new(value, value);
}

[TypeSerializer]
public sealed class IntRangeSerializer :
    ITypeSerializer<IntRange, ValueDataNode>,
    ITypeSerializer<IntRange, MappingDataNode>
{
    public ValidationNode Validate(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null
    ) =>
        serializationManager.ValidateNode<Dictionary<string, int>>(node, context);

    public IntRange Read(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<IntRange>? instanceProvider = null
    )
    {
        int? min = null;
        int? max = null;

        if (node.TryGet("min", out var minNode) && minNode is ValueDataNode minVal &&
            int.TryParse(minVal.Value, out var parsedMin))
            min = parsedMin;

        if (node.TryGet("max", out var maxNode) && maxNode is ValueDataNode maxVal &&
            int.TryParse(maxVal.Value, out var parsedMax))
            max = parsedMax;

        var a = min ?? max ?? 0;
        var b = max ?? min ?? a;

        if (b < a)
            (a, b) = (b, a);

        return new(a, b);
    }

    public ValidationNode Validate(
        ISerializationManager serializationManager,
        ValueDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null
    ) =>
        serializationManager.ValidateNode<int>(node, context);

    public IntRange Read(
        ISerializationManager serializationManager,
        ValueDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<IntRange>? instanceProvider = null
    )
    {
        if (!int.TryParse(node.Value, out var v))
            v = 0;

        return new(v, v);
    }

    public DataNode Write(
        ISerializationManager serializationManager,
        IntRange value,
        IDependencyCollection dependencies,
        bool alwaysWrite = false,
        ISerializationContext? context = null
    )
    {
        if (value.Min == value.Max)
            return new ValueDataNode(value.Min.ToString());

        var map = new MappingDataNode();
        map.Add("min", new ValueDataNode(value.Min.ToString()));
        map.Add("max", new ValueDataNode(value.Max.ToString()));
        return map;
    }
}
