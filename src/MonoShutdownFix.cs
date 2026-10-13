using System;
using System.Reflection;

namespace BJCCheat
{
    /// <summary>
    /// Works around the crash on game exit (access violation in mono_domain_free).
    /// MonoMod 22.01 (shipped with BepInEx 5.4.23) sets "corlib_internal" on every assembly it
    /// generates for Harmony patches, using a hardcoded MonoAssembly offset. In Unity 2022.3's
    /// Mono (x86) that byte lands in the high byte of friend_assembly_names, turning NULL into
    /// 0x01000000, which Mono then tries to free during shutdown. We put the NULL back.
    /// </summary>
    public static class MonoShutdownFix
    {
        // MonoAssembly layout of Unity 2022.3 Mono, 32-bit (verified against the crash dump)
        private const int ImageOffset = 0x48;
        private const int FriendNamesOffset = 0x4C;
        private const uint CorruptedNull = 0x01000000;

        public static unsafe int Apply()
        {
            if (IntPtr.Size != 4) return 0;

            int repaired = 0;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    IntPtr monoAsm = GetPtr(asm, "_mono_assembly");
                    IntPtr image = GetPtr(asm.ManifestModule, "_impl");
                    if (monoAsm == IntPtr.Zero || image == IntPtr.Zero) continue;

                    byte* p = (byte*)monoAsm;
                    // Only touch structs whose layout we recognise
                    if (*(IntPtr*)(p + ImageOffset) != image) continue;

                    uint* friendNames = (uint*)(p + FriendNamesOffset);
                    if (*friendNames == CorruptedNull)
                    {
                        *friendNames = 0;
                        repaired++;
                    }
                }
                catch { }
            }
            return repaired;
        }

        private static IntPtr GetPtr(object obj, string fieldName)
        {
            for (var t = obj?.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null) return f.GetValue(obj) is IntPtr ptr ? ptr : IntPtr.Zero;
            }
            return IntPtr.Zero;
        }
    }
}
