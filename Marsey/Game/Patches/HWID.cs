using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using Marsey.Config;
using Marsey.Handbreak;
using Marsey.Misc;
using Marsey.Stealthsey;

namespace Marsey.Game.Patches;

public static class HWID
{
    private static byte[] _hwId2 = Array.Empty<byte>();
    private static byte[] _hwId = Array.Empty<byte>();
    private static string _flYi = string.Empty; // flYi = GUID

    /// <summary>
    /// Patching the HWId function and replacing it with a custom HWId.
    /// </summary>
    public static void Force()
    {
        if (!MarseyConf.ForceHWID)
        {
            MarseyLogger.Log(MarseyLogger.LogType.INFO, "HWIDForcer", "Spoofer disabled.");
            return;
        }

        PatchCalcMethod();
    }

    public static void SetModern(string value)
    {
        _hwId2 = ConvertToBytes(value);
    }

    public static void SetLegacy(string value)
    {
        _hwId = ConvertToBytes(value);
    }

    public static void SetFlYi(string guid)
    {
        _flYi = guid;
    }

    public static void SetAll(string modern, string legacy, string adminGuid)
    {
        SetModern(modern);
        SetLegacy(legacy);
        SetFlYi(adminGuid);
    }

    private static string CleanHwid(string hwid)
    {
        return new string(hwid.Where(c => "0123456789ABCDEF".Contains(c)).ToArray());
    }

    private static byte[] ConvertToBytes(string hex)
    {
        string cleaned = CleanHwid(hex);
        if (cleaned.Length == 0)
            return Array.Empty<byte>();

        return Enumerable.Range(0, cleaned.Length / 2)
                          .Select(x => Convert.ToByte(cleaned.Substring(x * 2, 2), 16))
                          .ToArray();
    }

    public static string GenerateRandom(int length = 64)
    {
        Random random = new Random();
        const string chars = "0123456789ABCDEF";
        StringBuilder result = new StringBuilder(length);

        for (int i = 0; i < length; i++)
        {
            result.Append(chars[random.Next(chars.Length)]);
        }

        return result.ToString();
    }

    private static byte[] GenerateRandomBytes(int length = 32)
    {
        Random random = new Random();
        byte[] bytes = new byte[length];
        random.NextBytes(bytes);
        return bytes;
    }

    private static void PatchCalcMethod()
    {
        Type? basicHwid = Helpers.TypeFromQualifiedName("Robust.Client.HWId.BasicHWId");
        Type? dummyHwid = Helpers.TypeFromQualifiedName("Robust.Shared.Network.DummyHWId");
        Type? flYi = Helpers.TypeFromQualifiedName("Content.Client.Administration.Systems.AdminInfoSystem");

        if (basicHwid is null && dummyHwid is null)
        {
            MarseyLogger.Log(MarseyLogger.LogType.ERRO, "HWIdForcer", "No HWId types found!");
            return;
        }
        if (flYi is null) MarseyLogger.Log(MarseyLogger.LogType.WARN, "flYiForcer", "flYi type not found!");

        if (basicHwid is not null)
        {
            if (OperatingSystem.IsWindows())
            {
                Helpers.PatchMethod(
                    basicHwid,
                    "GetLegacy",
                    typeof(HWID),
                    nameof(RecalcHwid),
                    HarmonyPatchType.Postfix
                );
            }

            Helpers.PatchMethod(
                basicHwid,
                "GetModern",
                typeof(HWID),
                nameof(RecalcHwid2),
                HarmonyPatchType.Postfix
            );
        }

        if (dummyHwid is not null)
        {
            if (OperatingSystem.IsWindows())
            {
                Helpers.PatchMethod(
                    dummyHwid,
                    "GetLegacy",
                    typeof(HWID),
                    nameof(RecalcHwid),
                    HarmonyPatchType.Postfix
                );
            }

            Helpers.PatchMethod(
                dummyHwid,
                "GetModern",
                typeof(HWID),
                nameof(RecalcHwid2),
                HarmonyPatchType.Postfix
            );
        }

        if (flYi is not null)
        {
            Helpers.PatchMethod(
                flYi,
                "i",
                typeof(HWID),
                nameof(PrefixAdminInfo),
                HarmonyPatchType.Prefix
            );
        }
    }

    public static bool CheckHWID(string hwid)
    {
        return Regex.IsMatch(hwid, "^$|^[A-F0-9]{64}$");
    }
    private static void RecalcHwid(ref byte[] __result)
    {
        string original = BitConverter.ToString(__result).Replace("-", "");

        if (_hwId.Length == 0)
        {
            _hwId = GenerateRandomBytes(32);
            MarseyLogger.Log(MarseyLogger.LogType.INFO, "HWIdForcer", "[LEGACY] No HWID provided, generated random");
        }

        string applied = BitConverter.ToString(_hwId).Replace("-", "");

        MarseyLogger.Log(MarseyLogger.LogType.INFO, "HWIdForcer", $"[LEGACY] Original: {original}");
        MarseyLogger.Log(MarseyLogger.LogType.INFO, "HWIdForcer", $"[LEGACY] Applied: {applied}");

        __result = _hwId;
    }

    private static void RecalcHwid2(ref byte[] __result)
    {
        string original = BitConverter.ToString(__result).Replace("-", "");

        if (_hwId2.Length == 0)
        {
            _hwId2 = GenerateRandomBytes(32);
            MarseyLogger.Log(MarseyLogger.LogType.INFO, "HWIdForcer", "[MODERN] No HWID provided, generated random");
        }

        string applied = BitConverter.ToString(_hwId2).Replace("-", "");

        MarseyLogger.Log(MarseyLogger.LogType.INFO, "HWIdForcer", $"[MODERN] Original: {original}");
        MarseyLogger.Log(MarseyLogger.LogType.INFO, "HWIdForcer", $"[MODERN] Applied: {applied}");

        __result = [0, .._hwId2];
    }
    public static bool CheckGuid(string guid)
    {
        return Guid.TryParse(guid, out _);
    }

    private static bool PrefixAdminInfo(ref Guid p)
    {
        string original = p.ToString();

        if (string.IsNullOrEmpty(_flYi))
        {
            MarseyLogger.Log(MarseyLogger.LogType.WARN, "flYiForcer", "[ADMIN GUID] No GUID provided, using original");
            return true;
        }

        if (Guid.TryParse(_flYi, out var spoofedGuid))
        {
            MarseyLogger.Log(MarseyLogger.LogType.INFO, "flYiForcer", $"[ADMIN GUID] Original: {original}");
            MarseyLogger.Log(MarseyLogger.LogType.INFO, "flYiForcer", $"[ADMIN GUID] Applied: {_flYi}");

            p = spoofedGuid;
        }
        else
        {
            MarseyLogger.Log(MarseyLogger.LogType.ERRO, "flYiForcer", $"[ADMIN GUID] Invalid GUID format: {_flYi}");
        }

        return true;
    }
}
