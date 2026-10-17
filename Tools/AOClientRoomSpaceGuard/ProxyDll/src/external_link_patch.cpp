#include "external_link_patch.h"

#include "logging.h"

#include <windows.h>

#include <cstring>

namespace
{
    // GUI.dll BrowserModule_c::AweOpenExternalLinkCallback, client build whose
    // GUI.dll is the 2023-06-02 18.8.50 image (RVA 0x10B6BC).
    //
    // Awesomium calls this for target="_blank". The function ShellExecuteA's
    // a URL that starts with http:// or https://, but only after
    // BrowserView::GetExternalLinksEnabled. The Store window leaves that flag
    // off, so the click returns before ShellExecuteA. The six-byte je below
    // is that flag check. NOP'ing it still runs the http(s) test. chatcmd and
    // ordinary in-window navigations use a different callback and are not here.
    //
    // 8B 48 10                 mov ecx, [eax+10h]
    // E8 52 17 00 00           call GetExternalLinksEnabled
    // 84 C0                    test al, al
    // 0F 84 BB 00 00 00        je  skip_shellexecute
    // 56                       push esi
    // FF 75 0C                 push [ebp+0Ch]
    // 8D 45 D0                 lea eax, [ebp-30h]
    // 50                       push eax
    constexpr uint8_t Signature[] = {
        0x8B, 0x48, 0x10,
        0xE8, 0x52, 0x17, 0x00, 0x00,
        0x84, 0xC0,
        0x0F, 0x84, 0xBB, 0x00, 0x00, 0x00,
        0x56,
        0xFF, 0x75, 0x0C,
        0x8D, 0x45, 0xD0,
        0x50
    };
    constexpr size_t GateOffset = 10;
    constexpr size_t GateSize = 6;
    constexpr uint8_t Nops[GateSize] = { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 };

    int CountMatches(const uint8_t* base, size_t size)
    {
        if (!base || size < sizeof(Signature))
        {
            return 0;
        }

        int count = 0;
        for (size_t offset = 0; offset + sizeof(Signature) <= size; ++offset)
        {
            if (std::memcmp(base + offset, Signature, sizeof(Signature)) == 0)
            {
                ++count;
            }
        }
        return count;
    }

    bool WriteNops(uint8_t* site)
    {
        DWORD oldProtection = 0;
        if (!VirtualProtect(site + GateOffset, GateSize, PAGE_EXECUTE_READWRITE, &oldProtection))
        {
            return false;
        }

        std::memcpy(site + GateOffset, Nops, GateSize);
        DWORD ignored = 0;
        VirtualProtect(site + GateOffset, GateSize, oldProtection, &ignored);
        FlushInstructionCache(GetCurrentProcess(), site + GateOffset, GateSize);
        return std::memcmp(site + GateOffset, Nops, GateSize) == 0;
    }

    uint8_t* FindInImage(uint8_t* base, size_t size, int& count)
    {
        count = 0;
        uint8_t* match = nullptr;
        if (!base || size < sizeof(Signature))
        {
            return nullptr;
        }

        for (size_t offset = 0; offset + sizeof(Signature) <= size; ++offset)
        {
            if (std::memcmp(base + offset, Signature, sizeof(Signature)) != 0)
            {
                continue;
            }

            ++count;
            match = base + offset;
        }
        return count == 1 ? match : nullptr;
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
        if (nt->Signature != IMAGE_NT_SIGNATURE ||
            nt->FileHeader.Machine != IMAGE_FILE_MACHINE_I386 ||
            nt->FileHeader.NumberOfSections == 0 ||
            nt->FileHeader.NumberOfSections > 96)
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

            uint8_t* start = base + section[index].VirtualAddress;
            size_t size = section[index].Misc.VirtualSize;
            int sectionCount = 0;
            uint8_t* sectionMatch = FindInImage(start, size, sectionCount);
            count += sectionCount;
            if (sectionMatch)
            {
                match = sectionMatch;
            }
        }
        return count == 1 ? match : nullptr;
    }
}

namespace aorf
{
    bool PatchExternalLinkGateForTest(uint8_t* image, size_t size)
    {
        int count = 0;
        uint8_t* site = FindInImage(image, size, count);
        if (!site || count != 1)
        {
            return false;
        }
        return WriteNops(site);
    }

    bool RunExternalLinkPatchSelfTest()
    {
        uint8_t* image = static_cast<uint8_t*>(VirtualAlloc(
            nullptr,
            0x1000,
            MEM_COMMIT | MEM_RESERVE,
            PAGE_READWRITE));
        if (!image)
        {
            return false;
        }

        std::memcpy(image + 32, Signature, sizeof(Signature));
        const bool patched = PatchExternalLinkGateForTest(image, 0x1000);
        const bool nopsLanded = patched &&
            std::memcmp(image + 32 + GateOffset, Nops, GateSize) == 0;
        const bool signatureConsumed = CountMatches(image, 0x1000) == 0;

        std::memcpy(image + 32, Signature, sizeof(Signature));
        std::memcpy(image + 128, Signature, sizeof(Signature));
        const bool refusedDuplicate = !PatchExternalLinkGateForTest(image, 0x1000);
        const bool duplicateUntouched =
            std::memcmp(image + 32, Signature, sizeof(Signature)) == 0 &&
            std::memcmp(image + 128, Signature, sizeof(Signature)) == 0;

        VirtualFree(image, 0, MEM_RELEASE);
        return nopsLanded && signatureConsumed && refusedDuplicate && duplicateUntouched;
    }

    bool InstallExternalLinkPatch()
    {
        HMODULE gui = nullptr;
        for (int attempt = 0; attempt < 300 && !gui; ++attempt)
        {
            gui = GetModuleHandleW(L"GUI.dll");
            if (!gui)
            {
                Sleep(100);
            }
        }

        if (!gui)
        {
            Log("EXTERNALLINK patch=SKIP reason=gui_missing");
            return false;
        }

        int count = 0;
        uint8_t* site = FindInModule(gui, count);
        if (!site)
        {
            Log("EXTERNALLINK patch=SKIP reason=pattern count=%d", count);
            return false;
        }

        if (!WriteNops(site))
        {
            Log("EXTERNALLINK patch=FAILED reason=protect code=%lu", GetLastError());
            return false;
        }

        Log(
            "EXTERNALLINK patch=INSTALLED site=%p gate=nop http=ShellExecuteA",
            static_cast<void*>(site));
        return true;
    }
}
