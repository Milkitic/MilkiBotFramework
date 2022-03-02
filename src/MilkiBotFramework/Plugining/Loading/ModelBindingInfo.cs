using System;
using System.Collections.Generic;

namespace MilkiBotFramework.Plugining.Loading;

public class ModelBindingInfo
{
    public ModelBindingInfo(Type targetType, List<CommandParameterInfo> parameterInfos)
    {
        TargetType = targetType;
        ParameterInfos = parameterInfos;
    }

    public Type TargetType { get; }
    public List<CommandParameterInfo> ParameterInfos { get; }
}