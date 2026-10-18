#include <windows.h>
#include <cstdio>
#include <cstring>
#include "battlesuit_social_patch.h"
// Wear-check hook against the real Gamecode.dll (loaded without imports, nothing executed at load).
static int g_id = 0, g_flagCalls = 0, g_stat30 = 4, g_stat76 = 2;
static int __fastcall GetStat(void*, void*, int stat, int mode)
{
    if (mode != 2) return 0;
    if (stat == 702) return g_id;
    if (stat == 30) return g_stat30;
    if (stat == 76) return g_stat76;
    return 0;
}
static int __fastcall HasFlag(void*, void*, int) { ++g_flagCalls; return 0; }
typedef int(__fastcall* Check)(void*, void*, void*, int);
typedef int(__fastcall* Wear)(void*, void*, void*, int, int);
static unsigned g_zeroIdentity[4] = {};
struct FakeContainer { void* vt; void** slots; unsigned pad[3]; int size; unsigned more[8]; };
int main(int argc, char** argv)
{
    HMODULE gc = LoadLibraryExA(argv[1], nullptr, DONT_RESOLVE_DLL_REFERENCES);
    if (!gc) { std::printf("load failed %lu\n", GetLastError()); return 2; }
    auto* base = reinterpret_cast<unsigned char*>(gc);
    auto* site = base + 0x80AFF;
    auto* wear = base + 0x81D5F;
    // GameData ACGItem_t::zero import (unresolved here): point it at a local zero identity.
    auto** zeroImport = reinterpret_cast<void**>(base + 0x1547A4);
    DWORD old = 0; VirtualProtect(zeroImport, 4, PAGE_READWRITE, &old); *zeroImport = g_zeroIdentity;
    std::printf("before %02X %02X | wear %02X %02X %02X %02X %02X %02X %02X\n", site[0], site[5], wear[0], wear[1], wear[2], wear[3], wear[4], wear[5], wear[6]);
    if (!aorf::InstallBattlesuitSocialPatch()) { std::printf("install failed\n"); return 3; }
    std::printf("after  %02X %02X | wear %02X %02X %02X\n", site[0], site[5], wear[0], wear[5], wear[6]);
    int fails = 0;
    if (site[0] != 0xE9 || wear[0] != 0xE9 || wear[5] != 0x90 || wear[6] != 0x90) { std::printf("detour bytes wrong\n"); ++fails; }
    void* vt[32] = {}; vt[0x3C / 4] = (void*)&GetStat; vt[0x14 / 4] = (void*)&HasFlag;
    void* item[64] = { vt };
    Check fn = reinterpret_cast<Check>(site);
    g_id = 206140; g_flagCalls = 0;
    int r = fn(item, nullptr, nullptr, 1);
    std::printf("hook1 battlesuit 206140 -> %d, original ran=%d\n", r, g_flagCalls); if (r != 2 || g_flagCalls) ++fails;
    g_id = 12345; g_flagCalls = 0;
    r = fn(item, nullptr, nullptr, 1);
    std::printf("hook1 other 12345 -> %d, original ran=%d\n", r, g_flagCalls); if (r != 2 || g_flagCalls != 1) ++fails;

    // Real wear check, social flag (bit 1) set, armor item (stat30=4 wearable, ItemClass 2), no feedback.
    Wear wfn = reinterpret_cast<Wear>(wear);
    static unsigned char character[0x400] = {};
    *reinterpret_cast<unsigned*>(character + 0x138) = 0x2;
    g_id = 12345;
    r = wfn(item, nullptr, character, 0, 0);
    unsigned flags = *reinterpret_cast<unsigned*>(character + 0x138);
    std::printf("wear: social worn, no equipment -> %d (expect 0 refused), flags=%X\n", r, flags); if (r != 0 || flags != 2) ++fails;

    // Equipment with an empty container: the real GetItemAtSlot runs for all 64 slots, returns null.
    FakeContainer container = {}; container.size = 0;
    void* equipment[4] = { nullptr, nullptr, &container, nullptr };
    *reinterpret_cast<void**>(character + 0x1B8) = equipment;
    r = wfn(item, nullptr, character, 0, 0);
    flags = *reinterpret_cast<unsigned*>(character + 0x138);
    std::printf("wear: social worn, empty equipment (real getter x64) -> %d (expect 0), flags=%X\n", r, flags); if (r != 0 || flags != 2) ++fails;

    std::printf(fails ? "FAIL\n" : "PASS real Gamecode.dll: both detours, wear trampoline, real slot getter\n");
    return fails;
}
