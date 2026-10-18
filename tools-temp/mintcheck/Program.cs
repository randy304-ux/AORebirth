using SmokeLounge.AOtomation.Messaging.GameData;
using ZoneEngine_New.Core.Inventory;
int fails = 0;
void Check(string name, Dictionary<CharacterStat, int> stats, int expect)
{
    var t = new ItemTemplate { Stats = stats };
    int got = t.MintStackCount;
    Console.WriteLine($"{name}: MintStackCount={got} expect={expect} {(got == expect ? "ok" : "FAIL")}");
    if (got != expect) fails++;
}
Check("KIWD 37 Hunter Crossbow (weapon, 212=12)", new() { [CharacterStat.ItemClass] = 1, [CharacterStat.MaxEnergy] = 12 }, 1);
Check("HSR Arms F-59 (weapon, 212=30)", new() { [CharacterStat.ItemClass] = 1, [CharacterStat.MaxEnergy] = 30 }, 1);
Check("Recharger (misc, 212=50)", new() { [CharacterStat.ItemClass] = 4, [CharacterStat.MaxEnergy] = 50 }, 50);
Check("Stim (no class, MultipleCount 10)", new() { [CharacterStat.MultipleCount] = 10 }, 10);
Check("Weapon with MultipleCount 5", new() { [CharacterStat.ItemClass] = 1, [CharacterStat.MaxEnergy] = 12, [CharacterStat.MultipleCount] = 5 }, 5);
Check("Plain armor", new() { [CharacterStat.ItemClass] = 2 }, 1);
Console.WriteLine(fails == 0 ? "PASS" : "FAIL");
return fails;
