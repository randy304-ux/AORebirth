#include "battlesuit_social_patch.h"

#include "logging.h"

#include <windows.h>

#include <cstring>

namespace
{
    // Gamecode.dll, 18.8.x client: the item check behind "Social armor requires that no other armor
    // is worn" (Feedback_SocialArmorNoOtherArmor). thiscall on the item being worn, two stack
    // arguments (character, show-feedback byte), ret 8. Returns 2 when allowed, 0 when refused.
    // It reads the item's stat 30 and, when the social-armor flag (0x100) is set, refuses if any
    // armor slot (16..31) holds an item without CanBeWornWithSocialArmor.
    //
    // 55                 push ebp
    // 8B EC              mov ebp, esp
    // 51 51 53 56 57     push ecx; push ecx; push ebx; push esi; push edi
    // 8B D9              mov ebx, ecx
    // 8B 03 6A 02 6A 1E  mov eax,[ebx]; push 2; push 1Eh
    // FF 50 3C           call [eax+3Ch]        ; GetStat(30, 2)
    // 8B 03 68 00 01 00 00 8B CB FF 50 14      ; flag test 0x100
    // 84 C0 74 72        test al, al; je allowed
    // C7 45 FC 10 00 00 00                      ; first armor slot 16
    // BF BE 02 00 00     mov edi, 2BEh         ; stat 702 ACGItemTemplateID
    constexpr uint8_t Signature[] = {
        0x55, 0x8B, 0xEC, 0x51, 0x51, 0x53, 0x56, 0x57, 0x8B, 0xD9,
        0x8B, 0x03, 0x6A, 0x02, 0x6A, 0x1E, 0xFF, 0x50, 0x3C,
        0x8B, 0x03, 0x68, 0x00, 0x01, 0x00, 0x00, 0x8B, 0xCB, 0xFF, 0x50, 0x14,
        0x84, 0xC0, 0x74, 0x72,
        0xC7, 0x45, 0xFC, 0x10, 0x00, 0x00, 0x00,
        0xBF, 0xBE, 0x02, 0x00, 0x00
    };
    constexpr size_t StolenBytes = 6;   // push ebp; mov ebp,esp; push ecx; push ecx; push ebx
    constexpr int StatTemplateId = 702;
    constexpr int StatTemplateId2 = 703;
    constexpr int Allowed = 2;

    // Same list as the zone engine's "battlesuit" appearance group: Armor class, Back slot, OnWear MonsterShape.
    constexpr int BattlesuitIds[] = {
        85935, 88067, 88068, 88069, 88070, 88071, 88072, 88073, 88074, 88075, 88076, 88077, 88078,
        142638, 160439, 160440,
        206136, 206137, 206138, 206139, 206140, 206141, 206142, 206143, 206144, 206145, 206146, 206147, 206148, 206149, 206150,
        267317, 283745, 284223, 284224, 284225, 284226, 284227, 284228
    };

    using CheckFn = int(__fastcall*)(void* item, void* edx, void* character, int feedback);
    using GetStatFn = int(__fastcall*)(void* self, void* edx, int stat, int mode);

    CheckFn Original = nullptr;
    LONG Logged = 0;

    bool IsBattlesuit(int id)
    {
        for (int known : BattlesuitIds)
        {
            if (known == id)
            {
                return true;
            }
        }
        return false;
    }

    int ReadStat(void* item, int stat)
    {
        void** vtable = *reinterpret_cast<void***>(item);
        auto getStat = reinterpret_cast<GetStatFn>(vtable[0x3C / sizeof(void*)]);
        return getStat(item, nullptr, stat, 2);
    }

    int __fastcall Hook(void* item, void* edx, void* character, int feedback)
    {
        if (item)
        {
            const int low = ReadStat(item, StatTemplateId);
            if (IsBattlesuit(low) || IsBattlesuit(ReadStat(item, StatTemplateId2)))
            {
                if (InterlockedIncrement(&Logged) <= 5)
                {
                    aorf::Log("BATTLESUIT social-armor check skipped item=%d", low);
                }
                return Allowed;
            }
        }
        return Original(item, edx, character, feedback);
    }

    uint8_t* FindInRange(uint8_t* start, size_t size, int& count)
    {
        uint8_t* match = nullptr;
        for (size_t offset = 0; size >= sizeof(Signature) && offset + sizeof(Signature) <= size; ++offset)
        {
            if (std::memcmp(start + offset, Signature, sizeof(Signature)) == 0)
            {
                ++count;
                match = start + offset;
            }
        }
        return match;
    }

    uint8_t* FindInModule(HMODULE module, int& count)
    {
        count = 0;
        auto* base = reinterpret_cast<uint8_t*>(module);
        auto* dos = reinterpret_cast<IMAGE_DOS_HEADER*>(base);
        if (!dos || dos->e_magic != IMAGE_DOS_SIGNATURE || dos->e_lfanew <= 0)
        {
            return nullptr;
        }
        auto* nt = reinterpret_cast<IMAGE_NT_HEADERS*>(base + dos->e_lfanew);
        if (nt->Signature != IMAGE_NT_SIGNATURE || nt->FileHeader.Machine != IMAGE_FILE_MACHINE_I386 ||
            nt->FileHeader.NumberOfSections == 0 || nt->FileHeader.NumberOfSections > 96)
        {
            return nullptr;
        }
        uint8_t* match = nullptr;
        auto* section = IMAGE_FIRST_SECTION(nt);
        for (unsigned index = 0; index < nt->FileHeader.NumberOfSections; ++index)
        {
            if ((section[index].Characteristics & IMAGE_SCN_MEM_EXECUTE) == 0)
            {
                continue;
            }
            uint8_t* found = FindInRange(base + section[index].VirtualAddress, section[index].Misc.VirtualSize, count);
            if (found)
            {
                match = found;
            }
        }
        return count == 1 ? match : nullptr;
    }

    // Copies the stolen prologue into an executable trampoline that jumps back after it,
    // then points the site at the hook. Returns the trampoline (the original function).
    void* Detour(uint8_t* site, void* hook)
    {
        auto* trampoline = static_cast<uint8_t*>(VirtualAlloc(nullptr, 32, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE));
        if (!trampoline)
        {
            return nullptr;
        }
        std::memcpy(trampoline, site, StolenBytes);
        trampoline[StolenBytes] = 0xE9;
        const int32_t back = static_cast<int32_t>((site + StolenBytes) - (trampoline + StolenBytes + 5));
        std::memcpy(trampoline + StolenBytes + 1, &back, 4);
        FlushInstructionCache(GetCurrentProcess(), trampoline, 32);

        DWORD oldProtection = 0;
        if (!VirtualProtect(site, StolenBytes, PAGE_EXECUTE_READWRITE, &oldProtection))
        {
            VirtualFree(trampoline, 0, MEM_RELEASE);
            return nullptr;
        }
        uint8_t patch[StolenBytes] = { 0xE9, 0, 0, 0, 0, 0x90 };
        const int32_t to = static_cast<int32_t>(static_cast<uint8_t*>(hook) - (site + 5));
        std::memcpy(patch + 1, &to, 4);
        std::memcpy(site, patch, StolenBytes);
        DWORD ignored = 0;
        VirtualProtect(site, StolenBytes, oldProtection, &ignored);
        FlushInstructionCache(GetCurrentProcess(), site, StolenBytes);
        return trampoline;
    }

    // ---- self-test: a fake item whose vtable slot 15 is GetStat, and a fake original check.
    int FakeId = 0;
    int FakeOriginalCalls = 0;

    int __fastcall FakeGetStat(void*, void*, int stat, int mode)
    {
        return (mode == 2 && stat == StatTemplateId) ? FakeId : 0;
    }

    int __fastcall FakeOriginal(void*, void*, void*, int)
    {
        ++FakeOriginalCalls;
        return 0;   // stock rule: refused
    }
}

namespace aorf
{
    bool RunBattlesuitSocialPatchSelfTest()
    {
        void* vtable[32] = {};
        vtable[0x3C / sizeof(void*)] = reinterpret_cast<void*>(&FakeGetStat);
        void* item[4] = { vtable, nullptr, nullptr, nullptr };

        CheckFn saved = Original;
        Original = &FakeOriginal;
        FakeOriginalCalls = 0;
        FakeId = 88071;    // High-Quality Desert Battle Suit
        const bool suitAllowed = Hook(item, nullptr, nullptr, 1) == Allowed && FakeOriginalCalls == 0;
        FakeId = 160440;   // Desert Scorpiod Battle Suit
        const bool suit2Allowed = Hook(item, nullptr, nullptr, 1) == Allowed && FakeOriginalCalls == 0;
        FakeId = 151895;   // not a battle suit: stock rule runs
        const bool otherRefused = Hook(item, nullptr, nullptr, 1) == 0 && FakeOriginalCalls == 1;
        Original = saved;

        // Signature: found and detoured exactly once in a fake image, refused when duplicated.
        auto* image = static_cast<uint8_t*>(VirtualAlloc(nullptr, 0x1000, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE));
        if (!image)
        {
            return false;
        }
        std::memcpy(image + 64, Signature, sizeof(Signature));
        int count = 0;
        uint8_t* site = FindInRange(image, 0x1000, count);
        bool detoured = false;
        if (site == image + 64 && count == 1)
        {
            auto* trampoline = static_cast<uint8_t*>(Detour(site, reinterpret_cast<void*>(&Hook)));
            detoured = trampoline && site[0] == 0xE9 && site[5] == 0x90 &&
                std::memcmp(trampoline, Signature, StolenBytes) == 0 && trampoline[StolenBytes] == 0xE9 &&
                std::memcmp(site + StolenBytes, Signature + StolenBytes, sizeof(Signature) - StolenBytes) == 0;
            if (trampoline)
            {
                VirtualFree(trampoline, 0, MEM_RELEASE);
            }
        }
        std::memset(image, 0, 0x1000);
        std::memcpy(image + 64, Signature, sizeof(Signature));
        std::memcpy(image + 512, Signature, sizeof(Signature));
        int dupCount = 0;
        FindInRange(image, 0x1000, dupCount);
        VirtualFree(image, 0, MEM_RELEASE);
        return suitAllowed && suit2Allowed && otherRefused && detoured && dupCount == 2;
    }

    bool InstallBattlesuitSocialPatch()
    {
        HMODULE gamecode = nullptr;
        for (int attempt = 0; attempt < 300 && !gamecode; ++attempt)
        {
            gamecode = GetModuleHandleW(L"Gamecode.dll");
            if (!gamecode)
            {
                Sleep(100);
            }
        }
        if (!gamecode)
        {
            Log("BATTLESUIT patch=SKIP reason=gamecode_missing");
            return false;
        }
        int count = 0;
        uint8_t* site = FindInModule(gamecode, count);
        if (!site)
        {
            Log("BATTLESUIT patch=SKIP reason=pattern count=%d", count);
            return false;
        }
        void* trampoline = Detour(site, reinterpret_cast<void*>(&Hook));
        if (!trampoline)
        {
            Log("BATTLESUIT patch=FAILED reason=protect code=%lu", GetLastError());
            return false;
        }
        Original = reinterpret_cast<CheckFn>(trampoline);
        Log("BATTLESUIT patch=INSTALLED site=%p ids=%u", static_cast<void*>(site), static_cast<unsigned>(sizeof(BattlesuitIds) / sizeof(BattlesuitIds[0])));
        return true;
    }
}