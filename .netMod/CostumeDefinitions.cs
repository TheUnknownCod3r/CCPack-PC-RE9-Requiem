using System;
using System.Collections.Generic;

namespace RE3DotNet_CC
{
    public static class CostumeDefinitions
    {
        public sealed class CostumeVariant
        {
            public string Name { get; }
            public int? CostumeId { get; }
            public string? BodyId { get; }
            public string? HeadId { get; }
            public string? HairId { get; }

            public CostumeVariant(string name, int? costumeId, string? bodyId, string? headId, string? hairId)
            {
                Name = name;
                CostumeId = costumeId;
                BodyId = bodyId;
                HeadId = headId;
                HairId = hairId;
            }
        }

        private static readonly List<CostumeVariant> JillCostumes = new()
        {
            new("Jill Default", 0, "pl2000", "pl2001", "pl2005"),
            new("Jill S.T.A.R.S.", 1, "pl2010", "pl2011", "pl2015"),
            new("Jill Classic", 2, "pl2020", "pl2021", "pl2025"),
            new("Hospital Jill", 3, "pl2700", "pl2701", "pl2705"),
            new("Hospital Jill S.T.A.R.S.", 4, "pl2710", "pl2711", "pl2715"),
            new("Hospital Jill Classic", 5, "pl2720", "pl2721", "pl2725"),
            new("Dream Jill", null, "pl2800", null, null),
            new("Dream Jill S.T.A.R.S.", null, "pl2810", null, null),
            new("Dream Jill Classic", null, "pl2820", null, null),
            new("Dream/Zombie Jill", null, "pl2900", "pl2901", "pl2905"),
            new("Mirror Jill Default", null, "pl2910", "pl2911", null),
            new("Mirror Jill S.T.A.R.S.", null, "pl2920", null, "pl2925"),
            new("Mirror Jill Classic", null, "pl2930", null, null),
            new("Mirror Jill Zombie", null, "pl2940", "pl2941", null),
            new("Nicholai", null, "pl4000", "pl4005", "pl4001")
        };

        private static readonly List<CostumeVariant> CarlosCostumes = new()
        {
            new("Carlos Default", null, "pl0000", "pl0001", "pl0005"),
            new("Carlos Zombie", null, "pl8070", "pl8071", "pl8075"),
            new("Carlos Wounded", null, "pl8090", "pl8091", null),
            new("Carlos Classic", null, null, "pl0011", "pl0015")
        };

        public static IReadOnlyList<CostumeVariant> GetCostumesForCharacter(string characterName)
        {
            if (characterName.Equals("Carlos", StringComparison.OrdinalIgnoreCase))
                return CarlosCostumes;

            return JillCostumes;
        }
    }
}
