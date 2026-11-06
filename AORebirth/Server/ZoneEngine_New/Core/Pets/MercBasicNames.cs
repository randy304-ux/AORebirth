namespace ZoneEngine_New.Core.Pets
{
    using System;

    /// <summary>
    /// Plain single given names for Basic hired mercs. Twink/elite dungeon roster names
    /// (<see cref="MercRosterCatalog"/>) stay reserved for those unlocks and Twink-tier hires.
    /// </summary>
    public static class MercBasicNames
    {
        // Modest common AO-flavored single names; not the Twink "First \"Nick\" Last" list.
        static readonly string[] Male =
        [
            "Aaron", "Blake", "Cade", "Derek", "Ellis", "Finn", "Grant", "Hugh",
            "Ivan", "Joel", "Kurt", "Leon", "Marc", "Neil", "Owen", "Paul",
            "Quinn", "Ross", "Seth", "Todd", "Ulric", "Vince", "Wade", "Xander",
            "York", "Zane", "Bram", "Cole", "Drew", "Erik", "Fritz", "Gavin",
            "Hank", "Igor", "Jace", "Kane", "Lars", "Milo", "Nash", "Otto"
        ];

        static readonly string[] Female =
        [
            "Ada", "Beth", "Cora", "Dana", "Elsa", "Faye", "Gina", "Hana",
            "Iris", "Jade", "Kara", "Lena", "Mira", "Nina", "Olga", "Page",
            "Quinn", "Rita", "Sara", "Tess", "Uma", "Vera", "Willa", "Xena",
            "Yara", "Zara", "Bree", "Cleo", "Dina", "Elle", "Fran", "Gwen",
            "Hope", "Ivy", "Jill", "Kate", "Lara", "Meg", "Nora", "Pia"
        ];

        public static string Pick(bool female)
        {
            string[] pool = female ? Female : Male;
            return pool[Random.Shared.Next(pool.Length)];
        }

        /// <summary>True when <paramref name="core"/> is one of the plain basic names (mode tag already stripped).</summary>
        public static bool IsBasicName(string? core)
        {
            if (string.IsNullOrWhiteSpace(core))
                return false;
            foreach (string name in Male)
            {
                if (string.Equals(core, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            foreach (string name in Female)
            {
                if (string.Equals(core, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static bool? TryInferFemale(string? core)
        {
            if (string.IsNullOrWhiteSpace(core))
                return null;
            foreach (string name in Female)
            {
                if (string.Equals(core, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            foreach (string name in Male)
            {
                if (string.Equals(core, name, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return null;
        }
    }
}
