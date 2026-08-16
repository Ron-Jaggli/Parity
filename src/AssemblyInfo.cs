using MelonLoader;
using Parity;

[assembly: MelonInfo(typeof(ParityMod), ParityBuildInfo.Name, ParityBuildInfo.Version, ParityBuildInfo.Author)]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace Parity
{
    internal static class ParityBuildInfo
    {
        public const string Name = "Parity";
        public const string Version = "1.2.0";
        public const string Author = "ron-jaggli";

        /// <summary>Prefix used for every line this mod writes to the console.</summary>
        public const string LogPrefix = "[Parity] ";
    }
}
