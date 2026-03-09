using System.Reflection;
using HarmonyLib;
using Marsey.Game.Managers;
using Marsey.Stealthsey;
using NUnit.Framework; // убедись, что есть
using System.Linq;
using System.Collections.Generic;

namespace Marsey.HideseyTests;

[TestFixture]
public class HideseyTest
{
    private string HarmonyID = "com.marsey.tests"; 
    private Harmony harm;
        
    [OneTimeSetUp]
    public void SetUp()
    {
        harm = new Harmony(HarmonyID);
        HarmonyManager.Init(harm);
        Hidesey.Initialize();
    }

    [Test]
    public void Hidesey_HiddenAssemblies()
    {
        // Arrange
        Hidesey.Disperse();
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        List<string> HiddenAssemblies = new List<string> { "Harmony", "Marsey", "MonoMod", "Mono.", "System.Reflection.Emit," };
        
        Assembly[] filteredAssemblies = assemblies.Where(assembly => 
            HiddenAssemblies.Any(forbidden => assembly.FullName != null && assembly.FullName.Contains(forbidden))
        ).ToArray();

        // Assert
        Assert.That(filteredAssemblies, Is.Empty, "Forbidden assemblies were found in the domain.");
    }
    
    [Test]
    public void Hidesey_HiddenTypes()
    {
        List<Type> allTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .ToList();
        
        List<string> HiddenTypeNamespaces = new List<string> { "Marsey", "Harmony" };
        
        Type[] filteredTypes = allTypes.Where(type => 
            HiddenTypeNamespaces.Any(forbidden => type.Namespace?.StartsWith(forbidden) ?? false)
        ).ToArray();
        
        // Assert
        Assert.That(filteredTypes, Is.Empty, "Forbidden types were found in the domain.");
    }
}
