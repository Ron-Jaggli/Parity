// Reference assembly for the interop projection of the game's mscorlib.
// This is the game's own garbage collector, which is a different heap from the
// one MelonLoader and code mods run on. See refs/README.md before editing.
namespace Il2CppSystem
{
    public static class GC
    {
        public static void Collect()
        {
        }
    }
}
