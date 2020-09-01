using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace FabrosYandexTaxi
{
    public static class ImageLibraryLicensePatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            yield return new CodeInstruction(OpCodes.Ret);
        }
    }
}