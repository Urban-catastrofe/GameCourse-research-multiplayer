using System;

namespace TankDemo.Networking
{
    /// <summary>Small helper for both game server and desktop client command lines.</summary>
    public static class CommandLine
    {
        public static string[] Args()
        {
            try { return Environment.GetCommandLineArgs(); }
            catch { return Array.Empty<string>(); }
        }

        public static bool Has(string[] args, string name)
        {
            return IndexOf(args, name) >= 0;
        }

        public static string Get(string[] args, string name)
        {
            int i = IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        public static int GetInt(string[] args, string name, int fallback)
        {
            return int.TryParse(Get(args, name), out int value) ? value : fallback;
        }

        public static ushort GetUShort(string[] args, string name, ushort fallback)
        {
            return ushort.TryParse(Get(args, name), out ushort value) && value > 0 ? value : fallback;
        }

        private static int IndexOf(string[] args, string name)
        {
            if (args == null)
                return -1;
            for (int i = 0; i < args.Length; i++)
                if (args[i] == name)
                    return i;
            return -1;
        }
    }
}
