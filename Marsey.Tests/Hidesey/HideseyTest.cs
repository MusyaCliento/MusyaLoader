using System;
using System.Reflection;
using HarmonyLib;
using Marsey.Game.Managers;
using Marsey.Stealthsey;
using NUnit.Framework; 
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
        Hidesey.HidePatch(typeof(Hidesey).Assembly);
    }

    [Test]
    public void Hidesey_HiddenAssemblies()
    {
        // Arrange
        Hidesey.Disperse();
        Assembly[] filteredAssemblies;

        using (Hidesey.ForceEngineView())
        {
            filteredAssemblies = AppDomain.CurrentDomain.GetAssemblies();
        }

        List<string> HiddenAssemblies = new List<string> { "Harmony", "Marsey", "MonoMod", "Mono." };
        
        Assembly[] foundForbidden = filteredAssemblies.Where(assembly => 
            assembly != Assembly.GetExecutingAssembly() &&
            HiddenAssemblies.Any(forbidden => assembly.FullName != null && assembly.FullName.Contains(forbidden))
        ).ToArray();

        // Assert
        Assert.That(foundForbidden, Is.Empty, "Forbidden assemblies were found in the engine view domain.");
    }
    
    [Test]
    public void Hidesey_HiddenTypes()
    {
        List<Type> allTypes = new List<Type>();

        using (Hidesey.ForceEngineView())
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly == Assembly.GetExecutingAssembly()) continue;

                try
                {
                    allTypes.AddRange(assembly.GetTypes());
                }
                catch (ReflectionTypeLoadException e)
                {
                    allTypes.AddRange(e.Types.Where(t => t != null)!);
                }
                catch
                {
                }
            }
        }
        
        List<string> HiddenTypeNamespaces = new List<string> { "Marsey", "Harmony" };
        
        Type[] filteredTypes = allTypes.Where(type => 
            HiddenTypeNamespaces.Any(forbidden => type.Namespace?.StartsWith(forbidden) ?? false)
        ).ToArray();
        
        // Assert
        Assert.That(filteredTypes, Is.Empty, "Forbidden types were found in the engine view domain.");
    }

    [Test]
    public void Hidesey_ManifestHidesCvars()
    {
        Hidesey.Apply(new HideManifest {
            Cvars = { "stealth_cvar" }
        });

        List<string> originalCvars = new List<string> { "normal_cvar", "stealth_cvar", "another_cvar" };
        IEnumerable<string> filteredCvars;

        // Act
        using (Hidesey.ForceEngineView())
        {
            filteredCvars = Hidesey.LyingCvars(originalCvars);
        }

        // Assert
        Assert.That(filteredCvars, Does.Contain("normal_cvar"));
        Assert.That(filteredCvars, Does.Not.Contain("stealth_cvar"), "Cvar wasn't hidden by the manifest.");
    }
}