#include <windows.h>
#include <cstdio>
#include "battlesuit_social_patch.h"
static int g_id = 0, g_flagCalls = 0, g_statCalls = 0;
static int __fastcall GetStat(void*, void*, int stat, int mode) { ++g_statCalls; return (stat == 702 && mode == 2) ? g_id : 0; }
static int __fastcall HasFlag(void*, void*, int) { ++g_flagCalls; return 0; }
typedef int(__fastcall* Check)(void*, void*, void*, int);
int main(int argc, char** argv)
{
    HMODULE gc = LoadLibraryExA(argv[1], nullptr, DONT_RESOLVE_DLL_REFERENCES);
    if (!gc) { std::printf("load failed %lu\n", GetLastError()); return 2; }
    auto* site = reinterpret_cast<unsigned char*>(gc) + 0x80AFF;
    std::printf("before %02X %02X %02X\n", site[0], site[1], site[2]);
    if (!aorf::InstallBattlesuitSocialPatch()) { std::printf("install failed\n"); return 3; }
    std::printf("after  %02X (jmp=E9) %02X\n", site[0], site[5]);
    void* vt[32] = {}; vt[0x3C / 4] = (void*)&GetStat; vt[0x14 / 4] = (void*)&HasFlag;
    void* item[8] = { vt };
    Check fn = reinterpret_cast<Check>(site);
    int fails = 0;
    g_id = 206140; g_flagCalls = 0;
    int r = fn(item, nullptr, nullptr, 1);
    std::printf("battlesuit 206140 -> %d, original ran=%d\n", r, g_flagCalls); if (r != 2 || g_flagCalls) ++fails;
    g_id = 12345; g_flagCalls = 0;
    r = fn(item, nullptr, nullptr, 1);
    std::printf("other 12345 (not social) -> %d, original ran=%d\n", r, g_flagCalls); if (r != 2 || g_flagCalls != 1) ++fails;
    std::printf(fails ? "FAIL\n" : "PASS real Gamecode.dll hook + trampoline\n");
    return fails;
}
