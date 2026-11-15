using Microsoft.Xna.Framework;
namespace Ironfield;
public static partial class SelfTest
{
    static void DbgArc()
    {
        foreach (int seed in new[] { 2002, 2003, 2004 })
        {
            var c = Campaign.New(seed);
            var pilot = new Pilot(c);
            var seen = new int[4]; var t4 = new int[4];
            for (int week = 0; week < 22; week++)
            {
                pilot.Run(24 * 7);
                foreach (var p in c.S.Parties.Where(p => p.Kind == PartyKind.Lord && p.Faction >= 0))
                {
                    int a = Factions.Get(p.Faction).Arcana;
                    if (p.Companies.Any(k => Rules.RoleOf(k) == "casters")) seen[a]++;
                    if (p.Tier >= 4) t4[a]++;
                }
            }
            Console.WriteLine($"seed {seed}: circles {seen[1]}/{seen[2]}/{seen[3]} tier4 {t4[1]}/{t4[2]}/{t4[3]}");
        }
    }
}