using Marsey.Handbreak;
using System.Reflection;

namespace Marsey.Game.Resources.Reflection;

/// <summary>
/// Holds reflection data related to Resources
/// </summary>
public static class ResourceTypes
{
    public static Type? ProtoMan { get; private set; }
    public static Type? ResPath { get; private set; }
    public static PropertyInfo? ResPathCanonPath { get; private set; }
    
    public static void Initialize()
    {
        ProtoMan = Helpers.TypeFromQualifiedName("Robust.Shared.ContentPack.ResourceManager"); 
        ResPath = Helpers.TypeFromQualifiedName("Robust.Shared.Utility.ResPath");
        ResPathCanonPath = ResPath?.GetProperty("CanonPath");
    }
}
