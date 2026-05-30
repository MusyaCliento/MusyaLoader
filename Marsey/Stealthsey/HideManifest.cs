using System.Collections.Generic;

namespace Marsey.Stealthsey;

/// <summary>
/// Declarative description of what a patch wants hidden from the anticheat.
/// Built by patch code (usually from the loader Entry, before content load) and handed to
/// <see cref="Hidesey.Apply(HideManifest)"/>, which merges it into the per-category registries.
///
/// All entries are STRINGS, not Type references, on purpose: registration happens from the
/// loader Entry point BEFORE the target types are loaded into the AppDomain, so typeof() is
/// not available yet. Pass the fully-qualified type name (Type.FullName) for the type
/// categories, and the registered name for cvars/commands.
///
///   Systems  — EntitySystem FullName  (filtered out of EntitySystemManager.GetEntitySystemTypes)
///   Comps    — Component FullName     (filtered out of ComponentFactory.AllRegisteredTypes)
///                NOTE: a [NetworkedComponent] CANNOT be hidden — it participates in the
///                server-verified network component hash. Apply() registers the name, but the
///                unified type filter (IsHiddenType) refuses to hide networked components at
///                read time and logs a warning instead of risking a connect-time kick.
///   Ioc      — service/impl FullName  (filtered out of DependencyCollection.GetRegisteredTypes)
///   Cvars    — cvar name              (filtered out of IConfigurationManager.GetRegisteredCVars)
///   Commands — command name           (removed from IConsoleHost.AvailableCommands enumeration)
///   StackNames — substring to scrub from Environment.StackTrace lines.
///                USUALLY UNNECESSARY: assemblies hidden via HidePatch() are scrubbed from the
///                stack automatically (their name comes from the hidden-assembly list). Use this
///                only for a name that is NOT a hidden assembly — e.g. a namespace fragment.
///
/// Usage (declarative core):
///   Hidesey.Apply(new HideManifest {
///       Comps    = { "Content.Client.Mymod.MeleeComponent" },
///       Commands = { "stealthmin" },
///   });
///
/// Usage (thin wrappers for one-offs, same destination):
///   Hidesey.HideComp("Content.Client.Mymod.MeleeComponent");
///   Hidesey.HideCommand("stealthmin");
/// </summary>
public sealed class HideManifest
{
    public List<string> Systems    { get; init; } = new();
    public List<string> Comps      { get; init; } = new();
    public List<string> Ioc        { get; init; } = new();
    public List<string> Cvars      { get; init; } = new();
    public List<string> Commands   { get; init; } = new();
    public List<string> StackNames { get; init; } = new();
}
