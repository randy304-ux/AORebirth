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

    // The reverse rule (Feedback_SocialArmorAndOtherArmor): the item's "can this character wear it"
    // check, thiscall on the item, three stack arguments (character, show-feedback byte, flag byte),
    // ret 0Ch. Once a social-armor item is worn the character carries flag bit 1 at +138h, and every
    // armor or weapon without CanBeWornWithSocialArmor (stat 30 0x10000000) is refused, so armor
    // could not go on after a battle suit. The hook clears that bit for the call only while a battle
    // suit is equipped; the stock rule (and the hook above) still decide everything else.
    //
    // 55 8B EC 51 56 8B F1 8B 06 6A 02 6A 1E FF 50 3C   ; GetStat(30, 2)
    // 89 45 FC A8 04 75 07 33 C0 E9 B2 00 00 00          ; not wearable -> 0
    // 8B 06 53 57 6A 02 6A 4C 8B CE FF 50 3C             ; GetStat(76 ItemClass, 2)
    // 8B 7D 08 8B 8F 38 01 00 00 D1 E9 33 DB F6 C1 01    ; character flags bit 1
    // 74 25 38 5D 10 75 20 F7 45 FC 00 00 00 10 75 17    ; CanBeWornWithSocialArmor
    // 83 F8 03 74 12 83 F8 05 74 0D 38 5D 0C 74 5B 53    ; implants/spirits pass
    constexpr uint8_t WearSignature[] = {
        0x55, 0x8B, 0xEC, 0x51, 0x56, 0x8B, 0xF1, 0x8B, 0x06, 0x6A, 0x02, 0x6A, 0x1E, 0xFF, 0x50, 0x3C,
        0x89, 0x45, 0xFC, 0xA8, 0x04, 0x75, 0x07, 0x33, 0xC0, 0xE9, 0xB2, 0x00, 0x00, 0x00,
        0x8B, 0x06, 0x53, 0x57, 0x6A, 0x02, 0x6A, 0x4C, 0x8B, 0xCE, 0xFF, 0x50, 0x3C,
        0x8B, 0x7D, 0x08, 0x8B, 0x8F, 0x38, 0x01, 0x00, 0x00, 0xD1, 0xE9, 0x33, 0xDB, 0xF6, 0xC1, 0x01,
        0x74, 0x25, 0x38, 0x5D, 0x10, 0x75, 0x20, 0xF7, 0x45, 0xFC, 0x00, 0x00, 0x00, 0x10, 0x75, 0x17,
        0x83, 0xF8, 0x03, 0x74, 0x12, 0x83, 0xF8, 0x05, 0x74, 0x0D, 0x38, 0x5D, 0x0C, 0x74, 0x5B, 0x53
    };
    constexpr size_t WearStolenBytes = 7;   // push ebp; mov ebp,esp; push ecx; push esi; mov esi,ecx
    constexpr size_t CharacterFlagsOffset = 0x138;
    constexpr uint32_t SocialArmorWornFlag = 0x2;
    constexpr size_t CharacterEquipmentOffset = 0x1B8;
    // In the first check: mov ecx,[eax+8]; call GetItemAtSlot (bounds-checked, null when empty).
    constexpr size_t SlotGetterCallOffset = 0x4D;
    constexpr uint8_t SlotGetterCall[] = { 0x8B, 0x48, 0x08, 0xE8 };
    constexpr int SlotCount = 64;   // weapons, armor, implants, social

    // Same list as the zone engine's "battlesuit" appearance group: Armor class, Back slot, OnWear MonsterShape.
    constexpr int BattlesuitIds[] = {
        85935, 88067, 88068, 88069, 88070, 88071, 88072, 88073, 88074, 88075, 88076, 88077, 88078,
        142638, 160439, 160440,
        206136, 206137, 206138, 206139, 206140, 206141, 206142, 206143, 206144, 206145, 206146, 206147, 206148, 206149, 206150,
        267317, 283745, 284223, 284224, 284225, 284226, 284227, 284228
    };

    using CheckFn = int(__fastcall*)(void* item, void* edx, void* character, int feedback);
    using GetStatFn = int(__fastcall*)(void* self, void* edx, int stat, int mode);
    using WearFn = int(__fastcall*)(void* item, void* edx, void* character, int feedback, int flag);
    using SlotItemFn = void*(__fastcall*)(void* container, void* edx, int slot);

    CheckFn Original = nullptr;
    WearFn OriginalWear = nullptr;
    SlotItemFn GetItemAtSlot = nullptr;
    LONG Logged = 0;
    LONG WearLogged = 0;

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

    bool IsBattlesuitItem(void* item)
    {
        return item && (IsBattlesuit(ReadStat(item, StatTemplateId)) || IsBattlesuit(ReadStat(item, StatTemplateId2)));
    }

    int FindWornBattlesuitUnguarded(void* character)
    {
        auto* equipment = *reinterpret_cast<uint8_t**>(static_cast<uint8_t*>(character) + CharacterEquipmentOffset);
        if (!equipment || !GetItemAtSlot)
        {
            return 0;
        }
        void* container = *reinterpret_cast<void**>(equipment + 8);
        if (!container)
        {
            return 0;
        }
        for (int slot = 0; slot < SlotCount; ++slot)
        {
            void* worn = GetItemAtSlot(container, nullptr, slot);
            if (IsBattlesuitItem(worn))
            {
                return ReadStat(worn, StatTemplateId);
            }
        }
        return 0;
    }

    // Item id of the equipped battle suit, 0 when none (or when the walk faults: stock rule then).
    int FindWornBattlesuit(void* character)
    {
        __try
        {
            return FindWornBattlesuitUnguarded(character);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            return 0;
        }
    }

    int __fastcall WearHook(void* item, void* edx, void* character, int feedback, int flag)
    {
        if (character)
        {
            auto* flags = reinterpret_cast<volatile uint32_t*>(static_cast<uint8_t*>(character) + CharacterFlagsOffset);
            if ((*flags & SocialArmorWornFlag) != 0)
            {
                const int suit = FindWornBattlesuit(character);
                if (suit != 0)
                {
                    if (InterlockedIncrement(&WearLogged) <= 5)
                    {
                        aorf::Log("BATTLESUIT armor-with-suit check relaxed suit=%d", suit);
                    }
                    *flags &= ~SocialArmorWornFlag;
                    const int result = OriginalWear(item, edx, character, feedback, flag);
                    *flags |= SocialArmorWornFlag;
                    return result;
                }
            }
        }
        return OriginalWear(item, edx, character, feedback, flag);
    }

    uint8_t* FindInRange(uint8_t* start, size_t size, int& count, const uint8_t* pattern = Signature, size_t length = sizeof(Signature))
    {
        uint8_t* match = nullptr;
        for (size_t offset = 0; size >= length && offset + length <= size; ++offset)
        {
            if (std::memcmp(start + offset, pattern, length) == 0)
            {
                ++count;
                match = start + offset;
            }
        }
        return match;
    }

    uint8_t* FindInModule(HMODULE module, int& count, const uint8_t* pattern = Signature, size_t length = sizeof(Signature))
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
            uint8_t* found = FindInRange(base + section[index].VirtualAddress, section[index].Misc.VirtualSize, count, pattern, length);
            if (found)
            {
                match = found;
            }
        }
        return count == 1 ? match : nullptr;
    }

    // Copies the stolen prologue into an executable trampoline that jumps back after it,
    // then points the site at the hook. Returns the trampoline (the original function).
    void* Detour(uint8_t* site, void* hook, size_t stolen = StolenBytes)
    {
        auto* trampoline = static_cast<uint8_t*>(VirtualAlloc(nullptr, 32, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE));
        if (!trampoline)
        {
            return nullptr;
        }
        std::memcpy(trampoline, site, stolen);
        trampoline[stolen] = 0xE9;
        const int32_t back = static_cast<int32_t>((site + stolen) - (trampoline + stolen + 5));
        std::memcpy(trampoline + stolen + 1, &back, 4);
        FlushInstructionCache(GetCurrentProcess(), trampoline, 32);

        DWORD oldProtection = 0;
        if (!VirtualProtect(site, stolen, PAGE_EXECUTE_READWRITE, &oldProtection))
        {
            VirtualFree(trampoline, 0, MEM_RELEASE);
            return nullptr;
        }
        uint8_t patch[16];
        std::memset(patch, 0x90, sizeof(patch));
        patch[0] = 0xE9;
        const int32_t to = static_cast<int32_t>(static_cast<uint8_t*>(hook) - (site + 5));
        std::memcpy(patch + 1, &to, 4);
        std::memcpy(site, patch, stolen);
        DWORD ignored = 0;
        VirtualProtect(site, stolen, oldProtection, &ignored);
        FlushInstructionCache(GetCurrentProcess(), site, stolen);
        return trampoline;
    }

    // Resolves GetItemAtSlot from the call inside the first check (rel32 after the E8).
    SlotItemFn ResolveSlotGetter(uint8_t* firstSite)
    {
        uint8_t* call = firstSite + SlotGetterCallOffset;
        if (std::memcmp(call, SlotGetterCall, sizeof(SlotGetterCall)) != 0)
        {
            return nullptr;
        }
        int32_t rel = 0;
        std::memcpy(&rel, call + sizeof(SlotGetterCall), 4);
        return reinterpret_cast<SlotItemFn>(call + sizeof(SlotGetterCall) + 4 + rel);
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

    // Fake character for the wear hook: flags at +138h, equipment at +1B8h whose +8 is the container.
    uint8_t FakeCharacter[0x200] = {};
    void* FakeEquipment[4] = {};
    void* FakeWorn = nullptr;     // item returned for FakeWornSlot
    int FakeWornSlot = 51;
    int FakeWearCalls = 0;
    uint32_t FakeWearSawFlags = 0;

    void* __fastcall FakeGetItemAtSlot(void*, void*, int slot)
    {
        return slot == FakeWornSlot ? FakeWorn : nullptr;
    }

    int __fastcall FakeOriginalWear(void*, void*, void* character, int, int)
    {
        ++FakeWearCalls;
        FakeWearSawFlags = *reinterpret_cast<uint32_t*>(static_cast<uint8_t*>(character) + CharacterFlagsOffset);
        return (FakeWearSawFlags & SocialArmorWornFlag) ? 0 : Allowed;   // stock reverse rule
    }

    uint32_t FakeFlags()
    {
        return *reinterpret_cast<uint32_t*>(FakeCharacter + CharacterFlagsOffset);
    }

    void SetFakeFlags(uint32_t value)
    {
        *reinterpret_cast<uint32_t*>(FakeCharacter + CharacterFlagsOffset) = value;
    }

    bool RunWearSelfTest(void* item, void* suitItem)
    {
        WearFn savedWear = OriginalWear;
        SlotItemFn savedGetter = GetItemAtSlot;
        OriginalWear = &FakeOriginalWear;
        GetItemAtSlot = &FakeGetItemAtSlot;
        FakeEquipment[2] = FakeEquipment;   // +8: any non-null container
        void* equipment = FakeEquipment;
        std::memcpy(FakeCharacter + CharacterEquipmentOffset, &equipment, sizeof(equipment));

        // Battle suit worn in social slot 51 with the social flag set: armor allowed, flag restored.
        FakeWorn = suitItem;
        SetFakeFlags(0x42);
        FakeWearCalls = 0;
        const bool suitAllows = WearHook(item, nullptr, FakeCharacter, 1, 0) == Allowed && FakeWearCalls == 1 &&
            (FakeWearSawFlags & SocialArmorWornFlag) == 0 && FakeFlags() == 0x42;

        // Ordinary social armor worn (no battle suit): stock refusal, flag seen as set.
        FakeWorn = item;
        const bool otherRefused = WearHook(item, nullptr, FakeCharacter, 1, 0) == 0 && FakeWearCalls == 2 &&
            (FakeWearSawFlags & SocialArmorWornFlag) != 0 && FakeFlags() == 0x42;

        // No social armor worn: untouched pass-through.
        FakeWorn = suitItem;
        SetFakeFlags(0x40);
        const bool passThrough = WearHook(item, nullptr, FakeCharacter, 1, 0) == Allowed && FakeWearCalls == 3 &&
            FakeWearSawFlags == 0x40 && FakeFlags() == 0x40;

        // No equipment pointer: stock rule.
        std::memset(FakeCharacter + CharacterEquipmentOffset, 0, sizeof(void*));
        SetFakeFlags(0x2);
        const bool noEquipment = WearHook(item, nullptr, FakeCharacter, 1, 0) == 0 && FakeWearCalls == 4;

        OriginalWear = savedWear;
        GetItemAtSlot = savedGetter;
        return suitAllows && otherRefused && passThrough && noEquipment;
    }

    int __fastcall FakeGetStatSuit(void*, void*, int stat, int mode)
    {
        return (mode == 2 && stat == StatTemplateId) ? 88071 : 0;
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

        void* suitVtable[32] = {};
        suitVtable[0x3C / sizeof(void*)] = reinterpret_cast<void*>(&FakeGetStatSuit);
        void* suitItem[4] = { suitVtable, nullptr, nullptr, nullptr };
        FakeId = 151895;   // item being worn: ordinary armor
        const bool wearOk = RunWearSelfTest(item, suitItem);

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
        // Wear check: found once, 7-byte detour (jmp + two nops), trampoline holds the prologue.
        std::memset(image, 0, 0x1000);
        std::memcpy(image + 256, WearSignature, sizeof(WearSignature));
        int wearCount = 0;
        uint8_t* wearSite = FindInRange(image, 0x1000, wearCount, WearSignature, sizeof(WearSignature));
        bool wearDetoured = false;
        if (wearSite == image + 256 && wearCount == 1)
        {
            auto* trampoline = static_cast<uint8_t*>(Detour(wearSite, reinterpret_cast<void*>(&WearHook), WearStolenBytes));
            wearDetoured = trampoline && wearSite[0] == 0xE9 && wearSite[5] == 0x90 && wearSite[6] == 0x90 &&
                std::memcmp(trampoline, WearSignature, WearStolenBytes) == 0 && trampoline[WearStolenBytes] == 0xE9 &&
                std::memcmp(wearSite + WearStolenBytes, WearSignature + WearStolenBytes, sizeof(WearSignature) - WearStolenBytes) == 0;
            if (trampoline)
            {
                VirtualFree(trampoline, 0, MEM_RELEASE);
            }
        }

        // Slot getter: rel32 of the call at +4Dh in the first check.
        std::memset(image, 0, 0x1000);
        std::memcpy(image + 64, Signature, sizeof(Signature));
        std::memcpy(image + 64 + SlotGetterCallOffset, SlotGetterCall, sizeof(SlotGetterCall));
        const int32_t rel = 0x300 - static_cast<int32_t>(64 + SlotGetterCallOffset + sizeof(SlotGetterCall) + 4);
        std::memcpy(image + 64 + SlotGetterCallOffset + sizeof(SlotGetterCall), &rel, 4);
        const bool getterOk = reinterpret_cast<uint8_t*>(ResolveSlotGetter(image + 64)) == image + 0x300;
        image[64 + SlotGetterCallOffset + 3] = 0xE8 ^ 0xFF;
        const bool getterRejects = ResolveSlotGetter(image + 64) == nullptr;

        std::memset(image, 0, 0x1000);
        std::memcpy(image + 64, Signature, sizeof(Signature));
        std::memcpy(image + 512, Signature, sizeof(Signature));
        int dupCount = 0;
        FindInRange(image, 0x1000, dupCount);
        VirtualFree(image, 0, MEM_RELEASE);
        return suitAllowed && suit2Allowed && otherRefused && wearOk && detoured && wearDetoured &&
            getterOk && getterRejects && dupCount == 2;
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

        // Reverse rule: armor while a battle suit is worn. Independent of the first hook's result.
        GetItemAtSlot = ResolveSlotGetter(site);
        int wearCount = 0;
        uint8_t* wearSite = FindInModule(gamecode, wearCount, WearSignature, sizeof(WearSignature));
        if (!GetItemAtSlot || !wearSite)
        {
            Log("BATTLESUIT wear-patch=SKIP getter=%d count=%d", GetItemAtSlot ? 1 : 0, wearCount);
            return true;
        }
        void* wearTrampoline = Detour(wearSite, reinterpret_cast<void*>(&WearHook), WearStolenBytes);
        if (!wearTrampoline)
        {
            Log("BATTLESUIT wear-patch=FAILED reason=protect code=%lu", GetLastError());
            return true;
        }
        OriginalWear = reinterpret_cast<WearFn>(wearTrampoline);
        Log("BATTLESUIT wear-patch=INSTALLED site=%p getter=%p", static_cast<void*>(wearSite), reinterpret_cast<void*>(GetItemAtSlot));
        return true;
    }
}