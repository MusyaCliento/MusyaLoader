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



    [Test]
    public void Hidesey_HiddenExceptionStackTrace()
    {
        string mixedTrace = "   at Robust.Shared.GameObjects.EntitySystem.Update()\n" +
                            "   at HarmonyLib.PatchFunctions.UpdateWrapper()\n" +
                            "   at Marsey.Stealthsey.Hidesey.DoSomething()\n" +
                            "   at Robust.Client.BaseClient.Main()";

        // 1. Normal view (hidden caller) leaves stack trace unmodified
        string normalTrace = Hidesey.LyingStackTrace(mixedTrace);
        Assert.That(normalTrace, Is.EqualTo(mixedTrace),
            "Stack trace should remain unmodified for hidden callers.");

        // 2. Under engine view, hidden frames (Harmony, Marsey) are stripped
        using (Hidesey.ForceEngineView())
        {
            Hidesey.ClearStackTraceCache();

            string sanitized1 = Hidesey.LyingStackTrace(mixedTrace);
            Assert.That(sanitized1, Does.Not.Contain("HarmonyLib"));
            Assert.That(sanitized1, Does.Not.Contain("Marsey"));
            Assert.That(sanitized1, Does.Contain("Robust.Shared.GameObjects.EntitySystem.Update()"));
            Assert.That(sanitized1, Does.Contain("Robust.Client.BaseClient.Main()"));

            // 3. Test caching: subsequent call with same input should return the cached instance
            string sanitized2 = Hidesey.LyingStackTrace(mixedTrace);
            Assert.That(ReferenceEquals(sanitized1, sanitized2), Is.True,
                "LyingStackTrace must return cached string reference on repeat calls.");
        }

        // 4. Test actual Exception.StackTrace hook under engine view
        Exception ex;
        try
        {
            throw new InvalidOperationException("Test exception for stack trace verification");
        }
        catch (Exception caught)
        {
            ex = caught;
        }

        using (Hidesey.ForceEngineView())
        {
            string? stackTrace = ex.StackTrace;
            if (!string.IsNullOrEmpty(stackTrace))
            {
                Assert.That(stackTrace, Does.Not.Contain("0Harmony"));
                Assert.That(stackTrace, Does.Not.Contain("HarmonyLib"));
            }
        }
    }

    [Test]
    public void Hidesey_HiddenTypeAssembly()
    {
        Type hiddenType = typeof(Hidesey);

        // 1. Normal view: caller is hidden, returns real assembly
        Assert.That(hiddenType.Assembly.GetName().Name, Is.EqualTo("Marsey"),
            "Hidden caller should see real assembly name.");

        // 2. Engine view: Type.Assembly should be spoofed and counter incremented
        using (Hidesey.ForceEngineView())
        {
            int spoofCountBefore = HideseyPatches.TypeAssemblySpoofedCount;
            Assembly spoofed = hiddenType.Assembly;

            Assert.That(spoofed, Is.Not.Null);
            Assert.That(spoofed.GetName().Name, Is.Not.EqualTo("Marsey"),
                "Type.Assembly was not spoofed under engine view.");
            Assert.That(HideseyPatches.TypeAssemblySpoofedCount, Is.GreaterThan(spoofCountBefore),
                "TypeAssemblySpoofedCount should increment on spoofing.");

            // 3. Non-hidden type: remains unchanged
            Assert.That(typeof(string).Assembly, Is.EqualTo(typeof(object).Assembly),
                "Non-hidden type's assembly must not be spoofed.");
        }
    }
}