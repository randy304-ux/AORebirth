#include "aovk_chain.h"

#include "logging.h"

#include <windows.h>

namespace aorf
{
    bool LoadAovkReloaded()
    {
        wchar_t path[1024] = {};
        DWORD length = GetModuleFileNameW(
            nullptr,
            path,
            static_cast<DWORD>(sizeof(path) / sizeof(path[0])));
        if (length == 0 || length >= sizeof(path) / sizeof(path[0]))
        {
            Log("AOVK chain=SKIP reason=exe_path");
            return false;
        }

        wchar_t* slash = nullptr;
        for (wchar_t* cursor = path; *cursor; ++cursor)
        {
            if (*cursor == L'\\' || *cursor == L'/')
            {
                slash = cursor;
            }
        }
        if (!slash)
        {
            Log("AOVK chain=SKIP reason=exe_path");
            return false;
        }

        const wchar_t* name = L"aovk-reloaded.dll";
        size_t remain = sizeof(path) / sizeof(path[0]) - static_cast<size_t>(slash + 1 - path);
        if (wcscpy_s(slash + 1, remain, name) != 0)
        {
            Log("AOVK chain=SKIP reason=exe_path");
            return false;
        }

        DWORD attributes = GetFileAttributesW(path);
        if (attributes == INVALID_FILE_ATTRIBUTES || (attributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
        {
            Log("AOVK chain=SKIP reason=not_installed");
            return false;
        }

        HMODULE loaded = LoadLibraryW(path);
        if (!loaded)
        {
            Log("AOVK chain=FAILED code=%lu", GetLastError());
            return false;
        }

        Log("AOVK chain=LOADED module=%p", static_cast<void*>(loaded));
        return true;
    }
}
