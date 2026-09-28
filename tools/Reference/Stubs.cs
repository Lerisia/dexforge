// Stand-ins for the parts of the tool that belong to its windows, so that its generation code compiles exactly as it is.
// Nothing here takes part in generating: these are names for display, and the way the two personal tables are loaded.
namespace Pk3DSRNGTool
{
    internal static class StringItem
    {
        public static int language => 0;
        public static string[] naturestr = Enumerable.Repeat(string.Empty, 25).ToArray();
        public static string[] hpstr = Enumerable.Repeat(string.Empty, 18).ToArray();
        public static readonly string[] helditemStr = { "50%", "5%", "1%", "---" };
        public static string[] speciestr = Enumerable.Repeat(string.Empty, 1024).ToArray();
        public static readonly string[] NONE_STR = { "None", "None", "None", "None", "None", "None" };
        public static readonly string[] FacilityName = Enumerable.Repeat(string.Empty, 64).ToArray();
        public static readonly string[] TrainerName = Enumerable.Repeat(string.Empty, 64).ToArray();
    }
}
namespace Pk3DSRNGTool.Properties
{
    internal static class Resources
    {
        public static string Folder = "";
        public static byte[] personal_uu => File.ReadAllBytes(Path.Combine(Folder, "personal_uu"));
        public static byte[] personal_ao => File.ReadAllBytes(Path.Combine(Folder, "personal_ao"));
    }
}
