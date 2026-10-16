#include "daily_login_routing.h"

#include "logging.h"

#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#include <tlhelp32.h>

#include <algorithm>
#include <cctype>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <vector>

namespace aorf
{
    namespace
    {
        constexpr unsigned long AoRebirthLoginIpHostOrder =
            (2ul << 24) | (24ul << 16) | (96ul << 8) | 30ul;
        constexpr unsigned long AoRebirthLoginIpClientArgumentOrder =
            (30ul << 24) | (96ul << 16) | (24ul << 8) | 2ul;
        constexpr unsigned long AoRebirthLocalIpHostOrder =
            (127ul << 24) | 1ul;
        constexpr unsigned long AoRebirthLocalIpClientArgumentOrder =
            (1ul << 24) | 127ul;
        constexpr unsigned long AoRebirthLoginPort = 7500;
        // Dungeon Dogs: Store/Daily Rewards go to the server this client logs into (the IA launch
        // argument), never to the AO Rebirth public server. HTTP port 80 is moved to the account web port.
        char RouteTargetIp[16] = {};
        unsigned char RouteTargetOctets[4] = {};
        unsigned short RouteWebPort = 8098;
        // True when the host came from AO_DDOGS_WEB_HOST or the client-folder web file, not from the IA argument.
        bool RouteHostExplicit = false;
        constexpr const char* AoRebirthDailyLoginHost = "uwg.daily.icc-rk";

        using FnGetAddrInfoA = int(WSAAPI*)(PCSTR, PCSTR, const ADDRINFOA*, PADDRINFOA*);
        using FnGetAddrInfoW = int(WSAAPI*)(PCWSTR, PCWSTR, const ADDRINFOW*, PADDRINFOW*);
        using FnGetHostByName = hostent*(WSAAPI*)(const char*);
        using FnSend = int(WSAAPI*)(SOCKET, const char*, int, int);
        using FnConnect = int(WSAAPI*)(SOCKET, const sockaddr*, int);
        using FnGetProcAddress = FARPROC(WINAPI*)(HMODULE, LPCSTR);
        using FnWSASend = int(WSAAPI*)(
            SOCKET,
            LPWSABUF,
            DWORD,
            LPDWORD,
            DWORD,
            LPWSAOVERLAPPED,
            LPWSAOVERLAPPED_COMPLETION_ROUTINE);

        struct LaunchEndpoint
        {
            bool hasIp;
            bool hasPort;
            bool dottedIp;
            unsigned long ip;
            unsigned long port;
        };

        LONG WorkerStarted = 0;
        bool RoutingEnabled = false;
        HMODULE Ws2 = nullptr;
        FnGetAddrInfoA RealGetAddrInfoA = nullptr;
        FnGetAddrInfoW RealGetAddrInfoW = nullptr;
        FnGetHostByName RealGetHostByName = nullptr;
        FnSend RealSend = nullptr;
        FnConnect RealConnect = nullptr;
        FnWSASend RealWSASend = nullptr;
        FnGetProcAddress RealGetProcAddressKernel32 = nullptr;
        FnGetProcAddress RealGetProcAddressKernelBase = nullptr;

        int WSAAPI HookGetAddrInfoA(
            PCSTR nodeName,
            PCSTR serviceName,
            const ADDRINFOA* hints,
            PADDRINFOA* result);
        int WSAAPI HookGetAddrInfoW(
            PCWSTR nodeName,
            PCWSTR serviceName,
            const ADDRINFOW* hints,
            PADDRINFOW* result);
        hostent* WSAAPI HookGetHostByName(const char* name);
        int WSAAPI HookSend(SOCKET socket, const char* buffer, int length, int flags);
        int WSAAPI HookConnect(SOCKET socket, const sockaddr* name, int nameLength);
        FARPROC WINAPI HookGetProcAddress(HMODULE module, LPCSTR procName);
        int WSAAPI HookWSASend(
            SOCKET socket,
            LPWSABUF buffers,
            DWORD bufferCount,
            LPDWORD bytesSent,
            DWORD flags,
            LPWSAOVERLAPPED overlapped,
            LPWSAOVERLAPPED_COMPLETION_ROUTINE completionRoutine);

        std::string ToLower(std::string value)
        {
            std::transform(
                value.begin(),
                value.end(),
                value.begin(),
                [](unsigned char character)
                {
                    return static_cast<char>(std::tolower(character));
                });
            return value;
        }

        bool ParseUnsignedToken(const wchar_t* text, unsigned long& value, const wchar_t** end)
        {
            if (!text || !iswdigit(*text))
            {
                return false;
            }

            unsigned long parsed = 0;
            while (iswdigit(*text))
            {
                unsigned long digit = static_cast<unsigned long>(*text - L'0');
                if (parsed > (0xfffffffful - digit) / 10ul)
                {
                    return false;
                }

                parsed = parsed * 10ul + digit;
                ++text;
            }

            value = parsed;
            if (end)
            {
                *end = text;
            }

            return true;
        }

        bool ParseDottedIpToken(const wchar_t* text, unsigned long& value, const wchar_t** end)
        {
            unsigned long octets[4] = {};
            const wchar_t* cursor = text;
            for (int index = 0; index < 4; ++index)
            {
                if (!ParseUnsignedToken(cursor, octets[index], &cursor) || octets[index] > 255)
                {
                    return false;
                }

                if (index < 3)
                {
                    if (*cursor != L'.')
                    {
                        return false;
                    }
                    ++cursor;
                }
            }

            value =
                (octets[0] << 24) |
                (octets[1] << 16) |
                (octets[2] << 8) |
                octets[3];
            if (end)
            {
                *end = cursor;
            }
            return true;
        }

        bool IsTokenBoundary(wchar_t value)
        {
            return value == 0 || iswspace(value) || value == L'"';
        }

        bool TryParseLaunchEndpoint(const wchar_t* commandLine, LaunchEndpoint& endpoint)
        {
            endpoint = {};
            if (!commandLine)
            {
                return false;
            }

            const wchar_t* ia = wcsstr(commandLine, L"IA");
            while (ia)
            {
                unsigned long parsed = 0;
                const wchar_t* end = nullptr;
                bool dotted = ParseDottedIpToken(ia + 2, parsed, &end);
                if (!dotted)
                {
                    if (!ParseUnsignedToken(ia + 2, parsed, &end))
                    {
                        ia = wcsstr(ia + 2, L"IA");
                        continue;
                    }
                }

                if (IsTokenBoundary(*end))
                {
                    endpoint.hasIp = true;
                    endpoint.dottedIp = dotted;
                    endpoint.ip = parsed;
                    break;
                }

                ia = wcsstr(ia + 2, L"IA");
            }

            const wchar_t* ip = wcsstr(commandLine, L"IP");
            while (ip)
            {
                unsigned long parsed = 0;
                const wchar_t* end = nullptr;
                if (ParseUnsignedToken(ip + 2, parsed, &end) &&
                    IsTokenBoundary(*end) &&
                    parsed <= 65535)
                {
                    endpoint.hasPort = true;
                    endpoint.port = parsed;
                    break;
                }

                ip = wcsstr(ip + 2, L"IP");
            }

            return endpoint.hasIp && endpoint.hasPort;
        }

        bool IsAoRebirthEndpoint(const LaunchEndpoint& endpoint)
        {
            return endpoint.hasIp &&
                endpoint.hasPort &&
                (endpoint.ip == AoRebirthLoginIpHostOrder ||
                 endpoint.ip == AoRebirthLoginIpClientArgumentOrder ||
                 endpoint.ip == AoRebirthLocalIpHostOrder ||
                 endpoint.ip == AoRebirthLocalIpClientArgumentOrder) &&
                endpoint.port == AoRebirthLoginPort;
        }

        bool IsDailyLoginHost(const std::string& host)
        {
            std::string lower = ToLower(host);
            return lower == "dailyrewards.anarchy-online.com" ||
                lower == "uwg.daily.icc-rk" ||
                lower == "www.daily.icc-rk" ||
                lower == "uwg.store.icc-rk" ||
                lower == "www.store.icc-rk" ||
                lower == "aoshop.funcom.com" ||
                lower == "aomarket.funcom.com";
        }

        bool IsOrdinalProcedureName(LPCSTR procName)
        {
            return !procName ||
                ((reinterpret_cast<uintptr_t>(procName) & ~static_cast<uintptr_t>(0xffff)) == 0);
        }

        bool IsWs2Module(HMODULE module)
        {
            return module &&
                (module == Ws2 || module == GetModuleHandleW(L"ws2_32.dll"));
        }

        bool IsHttpRequestPrefix(const std::string& value)
        {
            return value.rfind("GET ", 0) == 0 ||
                value.rfind("POST ", 0) == 0 ||
                value.rfind("HEAD ", 0) == 0;
        }

        std::string RightPaddedHostReplacement()
        {
            constexpr const char* source = "dailyrewards.anarchy-online.com";
            std::string replacement = AoRebirthDailyLoginHost;
            replacement.append(std::strlen(source) - replacement.size(), ' ');
            return replacement;
        }

        bool ReplaceAll(std::string& value, const std::string& from, const std::string& to)
        {
            bool replaced = false;
            size_t offset = 0;
            while ((offset = value.find(from, offset)) != std::string::npos)
            {
                value.replace(offset, from.size(), to);
                offset += to.size();
                replaced = true;
            }
            return replaced;
        }

        bool RewriteDailyLoginHttpRequest(
            const char* buffer,
            int length,
            std::vector<char>& rewritten)
        {
            if (!buffer || length <= 0 || length > 65536)
            {
                return false;
            }

            std::string request(buffer, buffer + length);
            if (!IsHttpRequestPrefix(request))
            {
                return false;
            }

            std::string lower = ToLower(request);
            const bool matchedDailyRewards =
                lower.find("dailyrewards.anarchy-online.com") != std::string::npos;
            const bool matchedWww =
                lower.find("www.daily.icc-rk") != std::string::npos;
            const bool matchedUwg =
                lower.find("uwg.daily.icc-rk") != std::string::npos;
            const bool matchedStore =
                lower.find("aoshop.funcom.com") != std::string::npos ||
                lower.find("aomarket.funcom.com") != std::string::npos;
            if (matchedStore && !matchedDailyRewards && !matchedWww && !matchedUwg)
            {
                // Store/market requests keep their Host header (the account API serves the
                // Store hub for any non-daily host); only DNS and the port are redirected.
                Log("DAILYLOGIN route=http_store host_kept bytes=%lu", static_cast<unsigned long>(length));
                return false;
            }

            if (!matchedDailyRewards && !matchedWww && !matchedUwg)
            {
                return false;
            }

            bool changed = false;
            changed |= ReplaceAll(
                request,
                "Host: dailyrewards.anarchy-online.com",
                "Host: " + RightPaddedHostReplacement());
            changed |= ReplaceAll(
                request,
                "host: dailyrewards.anarchy-online.com",
                "host: " + RightPaddedHostReplacement());
            changed |= ReplaceAll(
                request,
                "http://dailyrewards.anarchy-online.com",
                "http://" + std::string(AoRebirthDailyLoginHost));
            changed |= ReplaceAll(
                request,
                "www.daily.icc-rk",
                AoRebirthDailyLoginHost);

            if (!changed)
            {
                return false;
            }

            rewritten.assign(request.begin(), request.end());
            Log(
                "DAILYLOGIN route=http_rewrite hostDailyRewards=%s hostWww=%s hostUwg=%s bytesIn=%lu bytesOut=%lu",
                matchedDailyRewards ? "true" : "false",
                matchedWww ? "true" : "false",
                matchedUwg ? "true" : "false",
                static_cast<unsigned long>(length),
                static_cast<unsigned long>(rewritten.size()));
            return true;
        }

        bool RewriteDailyLoginHttpRequestFromWsabufs(
            LPWSABUF buffers,
            DWORD bufferCount,
            std::vector<char>& rewritten)
        {
            if (!buffers || bufferCount == 0)
            {
                return false;
            }

            if (bufferCount == 1)
            {
                return RewriteDailyLoginHttpRequest(
                    buffers[0].buf,
                    static_cast<int>(buffers[0].len),
                    rewritten);
            }

            DWORD totalLength = 0;
            for (DWORD index = 0; index < bufferCount; ++index)
            {
                if (!buffers[index].buf ||
                    buffers[index].len > 65536 ||
                    totalLength > 65536 - buffers[index].len)
                {
                    return false;
                }

                totalLength += buffers[index].len;
            }

            std::vector<char> combined;
            combined.reserve(totalLength);
            for (DWORD index = 0; index < bufferCount; ++index)
            {
                combined.insert(
                    combined.end(),
                    buffers[index].buf,
                    buffers[index].buf + buffers[index].len);
            }

            return RewriteDailyLoginHttpRequest(
                combined.data(),
                static_cast<int>(combined.size()),
                rewritten);
        }

        FARPROC ResolveWs2(const char* name)
        {
            if (!Ws2)
            {
                Ws2 = LoadLibraryW(L"ws2_32.dll");
            }

            if (!Ws2)
            {
                return nullptr;
            }

            if (RealGetProcAddressKernel32)
            {
                return RealGetProcAddressKernel32(Ws2, name);
            }

            return GetProcAddress(Ws2, name);
        }

        bool ResolveKernelFunctions()
        {
            HMODULE kernel32 = GetModuleHandleW(L"kernel32.dll");
            HMODULE kernelBase = GetModuleHandleW(L"kernelbase.dll");

            RealGetProcAddressKernel32 = kernel32
                ? reinterpret_cast<FnGetProcAddress>(GetProcAddress(kernel32, "GetProcAddress"))
                : nullptr;
            RealGetProcAddressKernelBase = kernelBase
                ? reinterpret_cast<FnGetProcAddress>(GetProcAddress(kernelBase, "GetProcAddress"))
                : nullptr;

            return RealGetProcAddressKernel32 != nullptr;
        }

        bool ResolveWs2Functions()
        {
            if (!ResolveKernelFunctions())
            {
                return false;
            }

            RealGetAddrInfoA = reinterpret_cast<FnGetAddrInfoA>(ResolveWs2("getaddrinfo"));
            RealGetAddrInfoW = reinterpret_cast<FnGetAddrInfoW>(ResolveWs2("GetAddrInfoW"));
            RealGetHostByName = reinterpret_cast<FnGetHostByName>(ResolveWs2("gethostbyname"));
            RealSend = reinterpret_cast<FnSend>(ResolveWs2("send"));
            RealWSASend = reinterpret_cast<FnWSASend>(ResolveWs2("WSASend"));
            RealConnect = reinterpret_cast<FnConnect>(ResolveWs2("connect"));
            return RealGetAddrInfoA && RealGetAddrInfoW && RealGetHostByName && RealSend && RealWSASend && RealConnect;
        }

        bool PatchImport(HMODULE module, const char* importModule, const char* name, void* original, void* replacement)
        {
            __try
            {
                if (!module || !original || !replacement)
                {
                    return false;
                }

                auto* base = reinterpret_cast<unsigned char*>(module);
                auto* dos = reinterpret_cast<IMAGE_DOS_HEADER*>(base);
                if (dos->e_magic != IMAGE_DOS_SIGNATURE)
                {
                    return false;
                }

                auto* nt = reinterpret_cast<IMAGE_NT_HEADERS*>(base + dos->e_lfanew);
                if (nt->Signature != IMAGE_NT_SIGNATURE)
                {
                    return false;
                }

                IMAGE_DATA_DIRECTORY importDirectory =
                    nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
                if (importDirectory.VirtualAddress == 0)
                {
                    return false;
                }

                auto* descriptor = reinterpret_cast<IMAGE_IMPORT_DESCRIPTOR*>(
                    base + importDirectory.VirtualAddress);
                bool patched = false;
                for (; descriptor->Name; ++descriptor)
                {
                    const char* moduleName = reinterpret_cast<const char*>(base + descriptor->Name);
                    if (_stricmp(moduleName, importModule) != 0)
                    {
                        continue;
                    }

                    auto* thunk = reinterpret_cast<IMAGE_THUNK_DATA*>(
                        base + descriptor->FirstThunk);
                    auto* originalThunk = descriptor->OriginalFirstThunk
                        ? reinterpret_cast<IMAGE_THUNK_DATA*>(base + descriptor->OriginalFirstThunk)
                        : nullptr;
                    for (; thunk->u1.Function; ++thunk)
                    {
                        // Winsock 1 calls (connect, send, gethostbyname) are usually imported by
                        // ordinal; for those the bound address check below is the only match.
                        if (originalThunk &&
                            (originalThunk->u1.Ordinal & IMAGE_ORDINAL_FLAG))
                        {
                            ++originalThunk;
                        }
                        else if (originalThunk)
                        {
                            auto* importByName = reinterpret_cast<IMAGE_IMPORT_BY_NAME*>(
                                base + originalThunk->u1.AddressOfData);
                            if (std::strcmp(reinterpret_cast<const char*>(importByName->Name), name) != 0)
                            {
                                ++originalThunk;
                                continue;
                            }
                            ++originalThunk;
                        }

                        if (reinterpret_cast<void*>(thunk->u1.Function) != original)
                        {
                            continue;
                        }

                        DWORD oldProtection = 0;
                        if (!VirtualProtect(
                                &thunk->u1.Function,
                                sizeof(thunk->u1.Function),
                                PAGE_READWRITE,
                                &oldProtection))
                        {
                            continue;
                        }

                        thunk->u1.Function = reinterpret_cast<ULONG_PTR>(replacement);
                        DWORD ignored = 0;
                        VirtualProtect(
                            &thunk->u1.Function,
                            sizeof(thunk->u1.Function),
                            oldProtection,
                            &ignored);
                        patched = true;
                    }
                }

                return patched;
            }
            __except (EXCEPTION_EXECUTE_HANDLER)
            {
                return false;
            }
        }

        unsigned long PatchAllLoadedModules()
        {
            HANDLE snapshot = CreateToolhelp32Snapshot(
                TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32,
                GetCurrentProcessId());
            if (snapshot == INVALID_HANDLE_VALUE)
            {
                return 0;
            }

            MODULEENTRY32W entry = {};
            entry.dwSize = sizeof(entry);
            unsigned long patched = 0;
            if (Module32FirstW(snapshot, &entry))
            {
                do
                {
                    patched += PatchImport(entry.hModule, "WS2_32.dll", "getaddrinfo", RealGetAddrInfoA, reinterpret_cast<void*>(HookGetAddrInfoA)) ? 1ul : 0ul;
                    patched += PatchImport(entry.hModule, "WS2_32.dll", "GetAddrInfoW", RealGetAddrInfoW, reinterpret_cast<void*>(HookGetAddrInfoW)) ? 1ul : 0ul;
                    patched += PatchImport(entry.hModule, "WS2_32.dll", "gethostbyname", RealGetHostByName, reinterpret_cast<void*>(HookGetHostByName)) ? 1ul : 0ul;
                    patched += PatchImport(entry.hModule, "WS2_32.dll", "send", RealSend, reinterpret_cast<void*>(HookSend)) ? 1ul : 0ul;
                    patched += PatchImport(entry.hModule, "WS2_32.dll", "WSASend", RealWSASend, reinterpret_cast<void*>(HookWSASend)) ? 1ul : 0ul;
                    patched += PatchImport(entry.hModule, "WS2_32.dll", "connect", RealConnect, reinterpret_cast<void*>(HookConnect)) ? 1ul : 0ul;
                    patched += PatchImport(entry.hModule, "KERNEL32.dll", "GetProcAddress", RealGetProcAddressKernel32, reinterpret_cast<void*>(HookGetProcAddress)) ? 1ul : 0ul;
                    patched += PatchImport(entry.hModule, "KERNELBASE.dll", "GetProcAddress", RealGetProcAddressKernelBase, reinterpret_cast<void*>(HookGetProcAddress)) ? 1ul : 0ul;
                }
                while (Module32NextW(snapshot, &entry));
            }

            CloseHandle(snapshot);
            return patched;
        }

        bool IsPublicAoRebirthRoute()
        {
            return RouteTargetOctets[0] == 2 && RouteTargetOctets[1] == 24 &&
                RouteTargetOctets[2] == 96 && RouteTargetOctets[3] == 30;
        }

        void FormatRouteIp()
        {
            sprintf_s(RouteTargetIp, "%u.%u.%u.%u",
                static_cast<unsigned>(RouteTargetOctets[0]), static_cast<unsigned>(RouteTargetOctets[1]),
                static_cast<unsigned>(RouteTargetOctets[2]), static_cast<unsigned>(RouteTargetOctets[3]));
        }

        bool ParseDottedIpA(const char* text, unsigned char octets[4])
        {
            if (!text)
            {
                return false;
            }

            unsigned parts[4] = {};
            const char* cursor = text;
            for (int index = 0; index < 4; ++index)
            {
                if (!std::isdigit(static_cast<unsigned char>(*cursor)))
                {
                    return false;
                }

                unsigned value = 0;
                int digits = 0;
                while (std::isdigit(static_cast<unsigned char>(*cursor)))
                {
                    value = value * 10u + static_cast<unsigned>(*cursor - '0');
                    if (value > 255 || ++digits > 3)
                    {
                        return false;
                    }

                    ++cursor;
                }

                parts[index] = value;
                if (index < 3)
                {
                    if (*cursor != '.')
                    {
                        return false;
                    }

                    ++cursor;
                }
            }

            if (*cursor != 0)
            {
                return false;
            }

            for (int index = 0; index < 4; ++index)
            {
                octets[index] = static_cast<unsigned char>(parts[index]);
            }

            return true;
        }

        bool ApplyRouteHost(const char* host)
        {
            unsigned char octets[4] = {};
            if (ParseDottedIpA(host, octets))
            {
                std::memcpy(RouteTargetOctets, octets, 4);
                FormatRouteIp();
                return true;
            }

            using FnWsaStartup = int(WSAAPI*)(WORD, LPWSADATA);
            using FnFreeAddrInfoA = void(WSAAPI*)(ADDRINFOA*);
            auto startup = reinterpret_cast<FnWsaStartup>(ResolveWs2("WSAStartup"));
            auto release = reinterpret_cast<FnFreeAddrInfoA>(ResolveWs2("freeaddrinfo"));
            if (!host || !host[0] || !RealGetAddrInfoA || !startup || !release)
            {
                return false;
            }

            WSADATA wsa = {};
            startup(MAKEWORD(2, 2), &wsa);
            ADDRINFOA hints = {};
            hints.ai_family = AF_INET;
            ADDRINFOA* result = nullptr;
            if (RealGetAddrInfoA(host, nullptr, &hints, &result) != 0 || !result)
            {
                return false;
            }

            bool resolved = false;
            for (ADDRINFOA* entry = result; entry; entry = entry->ai_next)
            {
                if (entry->ai_family != AF_INET || !entry->ai_addr)
                {
                    continue;
                }

                const auto* in = reinterpret_cast<const sockaddr_in*>(entry->ai_addr);
                std::memcpy(RouteTargetOctets, &in->sin_addr, 4);
                resolved = true;
                break;
            }

            release(result);
            if (!resolved)
            {
                return false;
            }

            FormatRouteIp();
            return true;
        }

        bool ExtractJsonString(const std::string& text, const char* key, std::string& value)
        {
            const std::string needle = std::string("\"") + key + "\"";
            size_t pos = text.find(needle);
            if (pos == std::string::npos)
            {
                return false;
            }

            pos = text.find(':', pos + needle.size());
            if (pos == std::string::npos)
            {
                return false;
            }

            ++pos;
            while (pos < text.size() && std::isspace(static_cast<unsigned char>(text[pos])))
            {
                ++pos;
            }

            if (pos >= text.size())
            {
                return false;
            }

            if (text[pos] == '"')
            {
                ++pos;
                size_t end = text.find('"', pos);
                if (end == std::string::npos || end == pos)
                {
                    return false;
                }

                value = text.substr(pos, end - pos);
                return true;
            }

            size_t end = pos;
            while (end < text.size() && std::isdigit(static_cast<unsigned char>(text[end])))
            {
                ++end;
            }

            if (end == pos)
            {
                return false;
            }

            value = text.substr(pos, end - pos);
            return true;
        }

        bool ReadWholeFile(const wchar_t* path, std::string& out)
        {
            HANDLE file = CreateFileW(
                path,
                GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                nullptr,
                OPEN_EXISTING,
                FILE_ATTRIBUTE_NORMAL,
                nullptr);
            if (file == INVALID_HANDLE_VALUE)
            {
                return false;
            }

            DWORD size = GetFileSize(file, nullptr);
            if (size == INVALID_FILE_SIZE || size > 65536)
            {
                CloseHandle(file);
                return false;
            }

            out.assign(size, '\0');
            DWORD read = 0;
            BOOL ok = size == 0 || ReadFile(file, out.data(), size, &read, nullptr);
            CloseHandle(file);
            if (!ok)
            {
                return false;
            }

            out.resize(read);
            return true;
        }

        // Client folder file written by the launcher. Survives when the game drops env vars on relaunch.
        bool LoadWebFile(std::string& host, unsigned long& port, bool& hasPort)
        {
            host.clear();
            hasPort = false;
            wchar_t modulePath[MAX_PATH] = {};
            if (!GetModuleFileNameW(nullptr, modulePath, MAX_PATH))
            {
                return false;
            }

            wchar_t* slash = wcsrchr(modulePath, L'\\');
            if (!slash)
            {
                return false;
            }

            *(slash + 1) = 0;
            const wchar_t* names[] = { L"ddogs-web.json", L"config.json" };
            const std::wstring dir = modulePath;
            for (const wchar_t* name : names)
            {
                std::string text;
                if (!ReadWholeFile((dir + name).c_str(), text))
                {
                    continue;
                }

                std::string fileHost;
                const bool gotHost = ExtractJsonString(text, "AO_DDOGS_WEB_HOST", fileHost) ||
                    ExtractJsonString(text, "webHost", fileHost);
                std::string filePort;
                const bool gotPort = ExtractJsonString(text, "AO_DDOGS_WEB_PORT", filePort) ||
                    ExtractJsonString(text, "webPort", filePort);
                if (!gotHost && !gotPort)
                {
                    continue;
                }

                if (gotHost)
                {
                    host = fileHost;
                }

                if (gotPort)
                {
                    unsigned long parsed = std::strtoul(filePort.c_str(), nullptr, 10);
                    if (parsed > 0 && parsed <= 65535)
                    {
                        port = parsed;
                        hasPort = true;
                    }
                }

                return gotHost;
            }

            return false;
        }

        void ApplyWebPortOverride()
        {
            char portText[16] = {};
            DWORD portLength = GetEnvironmentVariableA("AO_DDOGS_WEB_PORT", portText, static_cast<DWORD>(sizeof(portText)));
            if (portLength > 0 && portLength < sizeof(portText))
            {
                unsigned long port = std::strtoul(portText, nullptr, 10);
                if (port > 0 && port <= 65535)
                {
                    RouteWebPort = static_cast<unsigned short>(port);
                }
            }
        }

        bool ResolveRouteTarget()
        {
            RouteHostExplicit = false;
            std::string fileHost;
            unsigned long filePort = 0;
            bool hasFilePort = false;
            const bool hasFileHost = LoadWebFile(fileHost, filePort, hasFilePort);
            const char* source = "none";

            char envHost[256] = {};
            DWORD envLength = GetEnvironmentVariableA("AO_DDOGS_WEB_HOST", envHost, static_cast<DWORD>(sizeof(envHost)));
            if (envLength > 0 && envLength < sizeof(envHost) && ApplyRouteHost(envHost))
            {
                RouteHostExplicit = true;
                source = "env";
            }
            else if (hasFileHost && ApplyRouteHost(fileHost.c_str()))
            {
                RouteHostExplicit = true;
                source = "file";
            }
            else
            {
                LaunchEndpoint endpoint = {};
                if (!TryParseLaunchEndpoint(GetCommandLineW(), endpoint))
                {
                    return false;
                }

                unsigned long ip = endpoint.ip;
                if (endpoint.dottedIp)
                {
                    RouteTargetOctets[0] = static_cast<unsigned char>((ip >> 24) & 0xff);
                    RouteTargetOctets[1] = static_cast<unsigned char>((ip >> 16) & 0xff);
                    RouteTargetOctets[2] = static_cast<unsigned char>((ip >> 8) & 0xff);
                    RouteTargetOctets[3] = static_cast<unsigned char>(ip & 0xff);
                }
                else
                {
                    // Integer IA is the in_addr read little-endian: the low byte is the first octet.
                    RouteTargetOctets[0] = static_cast<unsigned char>(ip & 0xff);
                    RouteTargetOctets[1] = static_cast<unsigned char>((ip >> 8) & 0xff);
                    RouteTargetOctets[2] = static_cast<unsigned char>((ip >> 16) & 0xff);
                    RouteTargetOctets[3] = static_cast<unsigned char>((ip >> 24) & 0xff);
                }

                FormatRouteIp();
                source = "launch";
            }

            if (hasFilePort)
            {
                RouteWebPort = static_cast<unsigned short>(filePort);
            }

            ApplyWebPortOverride();

            if (IsPublicAoRebirthRoute())
            {
                Log("DAILYLOGIN route=SKIP reason=aorebirth_public_server_not_routed");
                return false;
            }

            Log("DAILYLOGIN route=target source=%s ip=%s webPort=%u explicit=%s",
                source, RouteTargetIp, static_cast<unsigned>(RouteWebPort), RouteHostExplicit ? "true" : "false");
            return true;
        }

        bool ResolvePolicyAllowsRouting()
        {
            if (!ResolveRouteTarget())
            {
                Log("DAILYLOGIN route=SKIP reason=no_launch_target");
                return false;
            }

            char value[32] = {};
            DWORD length = GetEnvironmentVariableA(
                "AO_REBIRTH_DAILYLOGIN_ROUTING",
                value,
                static_cast<DWORD>(sizeof(value)));
            if (length > 0 && _stricmp(value, "Off") == 0)
            {
                Log("DAILYLOGIN route=SKIP reason=policy_off");
                return false;
            }

            if (RouteHostExplicit)
            {
                Log("DAILYLOGIN route=ARMED reason=web_host");
                return true;
            }

            if (length > 0 && _stricmp(value, "Always") == 0)
            {
                Log("DAILYLOGIN route=ARMED reason=policy_always");
                return true;
            }

            LaunchEndpoint endpoint = {};
            if (!TryParseLaunchEndpoint(GetCommandLineW(), endpoint))
            {
                Log("DAILYLOGIN route=SKIP reason=auto_endpoint_unavailable");
                return false;
            }

            if (!IsAoRebirthEndpoint(endpoint))
            {
                Log("DAILYLOGIN route=SKIP reason=auto_endpoint_not_aorebirth port=%lu", endpoint.port);
                return false;
            }

            Log("DAILYLOGIN route=ARMED reason=auto_endpoint_aorebirth port=%lu", endpoint.port);
            return true;
        }

        DWORD WINAPI DailyLoginRoutingWorker(LPVOID)
        {
            if (!ResolveWs2Functions())
            {
                Log("DAILYLOGIN route=BLOCKED reason=ws2_resolve");
                return 1;
            }

            RoutingEnabled = ResolvePolicyAllowsRouting();
            if (!RoutingEnabled)
            {
                return 0;
            }

            unsigned long totalPatches = 0;
            for (int attempt = 0; attempt < 300; ++attempt)
            {
                totalPatches += PatchAllLoadedModules();
                Sleep(200);
            }

            Log("DAILYLOGIN route=READY importPatchEvents=%lu", totalPatches);

            // Modules loaded after the first minute (the Store / Daily Rewards web view loads when the window is first
            // opened) were never patched, so their port-80 connect to the rerouted host was refused. Keep sweeping
            // for the whole session at a slower rate; already-patched imports no longer match and cost nothing.
            for (;;)
            {
                unsigned long late = PatchAllLoadedModules();
                if (late > 0)
                {
                    Log("DAILYLOGIN route=late_patch importPatchEvents=%lu", late);
                }

                Sleep(1000);
            }
        }

        int WSAAPI HookGetAddrInfoA(
            PCSTR nodeName,
            PCSTR serviceName,
            const ADDRINFOA* hints,
            PADDRINFOA* result)
        {
            if (RoutingEnabled && nodeName && IsDailyLoginHost(nodeName))
            {
                Log("DAILYLOGIN route=dns host=%s target=%s", nodeName, RouteTargetIp);
                return RealGetAddrInfoA(RouteTargetIp, serviceName, hints, result);
            }

            return RealGetAddrInfoA(nodeName, serviceName, hints, result);
        }

        int WSAAPI HookGetAddrInfoW(
            PCWSTR nodeName,
            PCWSTR serviceName,
            const ADDRINFOW* hints,
            PADDRINFOW* result)
        {
            if (RoutingEnabled && nodeName)
            {
                char host[256] = {};
                int converted = WideCharToMultiByte(
                    CP_ACP,
                    0,
                    nodeName,
                    -1,
                    host,
                    static_cast<int>(sizeof(host)),
                    nullptr,
                    nullptr);
                if (converted > 0 && IsDailyLoginHost(host))
                {
                    Log("DAILYLOGIN route=dnsw host=%s target=%s", host, RouteTargetIp);
                    wchar_t target[16] = {};
                    MultiByteToWideChar(CP_ACP, 0, RouteTargetIp, -1, target, 16);
                    return RealGetAddrInfoW(target, serviceName, hints, result);
                }
            }

            return RealGetAddrInfoW(nodeName, serviceName, hints, result);
        }

        hostent* WSAAPI HookGetHostByName(const char* name)
        {
            if (RoutingEnabled && name && IsDailyLoginHost(name))
            {
                struct HostStorage
                {
                    hostent entry;
                    char name[32];
                    char* aliases[1];
                    unsigned char address[4];
                    char* addressList[2];
                };

                thread_local HostStorage storage = {};
                strcpy_s(storage.name, RouteTargetIp);
                storage.aliases[0] = nullptr;
                std::memcpy(storage.address, RouteTargetOctets, 4);
                storage.addressList[0] = reinterpret_cast<char*>(storage.address);
                storage.addressList[1] = nullptr;
                storage.entry.h_name = storage.name;
                storage.entry.h_aliases = storage.aliases;
                storage.entry.h_addrtype = AF_INET;
                storage.entry.h_length = 4;
                storage.entry.h_addr_list = storage.addressList;
                Log("DAILYLOGIN route=gethostbyname host=%s target=%s", name, RouteTargetIp);
                return &storage.entry;
            }

            return RealGetHostByName(name);
        }

        int WSAAPI HookSend(SOCKET socket, const char* buffer, int length, int flags)
        {
            std::vector<char> rewritten;
            if (RoutingEnabled && RewriteDailyLoginHttpRequest(buffer, length, rewritten))
            {
                return RealSend(
                    socket,
                    rewritten.data(),
                    static_cast<int>(rewritten.size()),
                    flags);
            }

            return RealSend(socket, buffer, length, flags);
        }

        FARPROC WINAPI HookGetProcAddress(HMODULE module, LPCSTR procName)
        {
            if (RoutingEnabled && IsWs2Module(module) && !IsOrdinalProcedureName(procName))
            {
                if (std::strcmp(procName, "getaddrinfo") == 0)
                {
                    Log("DAILYLOGIN route=getproc name=getaddrinfo");
                    return reinterpret_cast<FARPROC>(HookGetAddrInfoA);
                }

                if (std::strcmp(procName, "GetAddrInfoW") == 0)
                {
                    Log("DAILYLOGIN route=getproc name=GetAddrInfoW");
                    return reinterpret_cast<FARPROC>(HookGetAddrInfoW);
                }

                if (std::strcmp(procName, "gethostbyname") == 0)
                {
                    Log("DAILYLOGIN route=getproc name=gethostbyname");
                    return reinterpret_cast<FARPROC>(HookGetHostByName);
                }

                if (std::strcmp(procName, "send") == 0)
                {
                    Log("DAILYLOGIN route=getproc name=send");
                    return reinterpret_cast<FARPROC>(HookSend);
                }

                if (std::strcmp(procName, "connect") == 0)
                {
                    Log("DAILYLOGIN route=getproc name=connect");
                    return reinterpret_cast<FARPROC>(HookConnect);
                }

                if (std::strcmp(procName, "WSASend") == 0)
                {
                    Log("DAILYLOGIN route=getproc name=WSASend");
                    return reinterpret_cast<FARPROC>(HookWSASend);
                }
            }

            return RealGetProcAddressKernel32
                ? RealGetProcAddressKernel32(module, procName)
                : nullptr;
        }

        int WSAAPI HookConnect(SOCKET socket, const sockaddr* name, int nameLength)
        {
            if (RoutingEnabled && name && nameLength >= static_cast<int>(sizeof(sockaddr_in)) &&
                name->sa_family == AF_INET)
            {
                const auto* in = reinterpret_cast<const sockaddr_in*>(name);
                if (in->sin_port == static_cast<USHORT>(80u << 8) &&
                    std::memcmp(&in->sin_addr, RouteTargetOctets, 4) == 0 &&
                    RouteWebPort != 80)
                {
                    sockaddr_in moved = *in;
                    moved.sin_port = static_cast<USHORT>(((RouteWebPort & 0xffu) << 8) | ((RouteWebPort >> 8) & 0xffu));
                    Log("DAILYLOGIN route=connect ip=%s port=80->%u", RouteTargetIp, static_cast<unsigned>(RouteWebPort));
                    return RealConnect(socket, reinterpret_cast<const sockaddr*>(&moved), static_cast<int>(sizeof(moved)));
                }
            }

            return RealConnect(socket, name, nameLength);
        }

        int WSAAPI HookWSASend(
            SOCKET socket,
            LPWSABUF buffers,
            DWORD bufferCount,
            LPDWORD bytesSent,
            DWORD flags,
            LPWSAOVERLAPPED overlapped,
            LPWSAOVERLAPPED_COMPLETION_ROUTINE completionRoutine)
        {
            std::vector<char> rewritten;
            WSABUF replacement = {};
            if (RoutingEnabled &&
                RewriteDailyLoginHttpRequestFromWsabufs(buffers, bufferCount, rewritten))
            {
                if (overlapped)
                {
                    char* heapBuffer = reinterpret_cast<char*>(
                        HeapAlloc(GetProcessHeap(), 0, rewritten.size()));
                    if (!heapBuffer)
                    {
                        return RealWSASend(
                            socket,
                            buffers,
                            bufferCount,
                            bytesSent,
                            flags,
                            overlapped,
                            completionRoutine);
                    }

                    std::memcpy(heapBuffer, rewritten.data(), rewritten.size());
                    replacement.buf = heapBuffer;
                    replacement.len = static_cast<ULONG>(rewritten.size());
                    return RealWSASend(
                        socket,
                        &replacement,
                        1,
                        bytesSent,
                        flags,
                        overlapped,
                        completionRoutine);
                }

                replacement.buf = rewritten.data();
                replacement.len = static_cast<ULONG>(rewritten.size());
                return RealWSASend(
                    socket,
                    &replacement,
                    1,
                    bytesSent,
                    flags,
                    nullptr,
                    completionRoutine);
            }

            return RealWSASend(
                socket,
                buffers,
                bufferCount,
                bytesSent,
                flags,
                overlapped,
                completionRoutine);
        }
    }

    bool StartDailyLoginRoutingWorker()
    {
        if (InterlockedCompareExchange(&WorkerStarted, 1, 0) != 0)
        {
            return true;
        }

        HANDLE worker = CreateThread(nullptr, 0, DailyLoginRoutingWorker, nullptr, 0, nullptr);
        if (!worker)
        {
            Log("DAILYLOGIN route=BLOCKED reason=worker_create code=%lu", GetLastError());
            return false;
        }

        CloseHandle(worker);
        return true;
    }

    bool RewriteDailyLoginHttpRequestForTest(
        const std::string& input,
        std::string& output)
    {
        std::vector<char> rewritten;
        if (!RewriteDailyLoginHttpRequest(
                input.data(),
                static_cast<int>(input.size()),
                rewritten))
        {
            return false;
        }

        output.assign(rewritten.begin(), rewritten.end());
        return true;
    }

    bool RunDailyLoginRoutingSelfTest()
    {
        LaunchEndpoint endpoint = {};
        if (!TryParseLaunchEndpoint(L"anarchyonline.exe IA2.24.96.30 IP7500 DUprivate", endpoint) ||
            !IsAoRebirthEndpoint(endpoint))
        {
            return false;
        }

        if (!TryParseLaunchEndpoint(L"anarchyonline.exe IA2.24.96.30 IP7505 DUofficial", endpoint) ||
            IsAoRebirthEndpoint(endpoint))
        {
            return false;
        }

        if (!IsDailyLoginHost("dailyrewards.anarchy-online.com") ||
            !IsDailyLoginHost("uwg.daily.icc-rk") ||
            !IsDailyLoginHost("www.daily.icc-rk") ||
            !IsDailyLoginHost("aoshop.funcom.com") ||
            !IsDailyLoginHost("aomarket.funcom.com") ||
            IsDailyLoginHost("register.funcom.com"))
        {
            return false;
        }

        std::string rewritten;
        if (!RewriteDailyLoginHttpRequestForTest(
                "GET / HTTP/1.1\r\nHost: dailyrewards.anarchy-online.com\r\n\r\n",
                rewritten) ||
            rewritten.find("GET / HTTP/1.1") == std::string::npos ||
            rewritten.find("Host: uwg.daily.icc-rk") == std::string::npos ||
            rewritten.size() != std::strlen("GET / HTTP/1.1\r\nHost: dailyrewards.anarchy-online.com\r\n\r\n"))
        {
            return false;
        }

        if (!RewriteDailyLoginHttpRequestForTest(
                "GET /index.app HTTP/1.1\r\nHost: www.daily.icc-rk\r\n\r\n",
                rewritten) ||
            rewritten.find("Host: uwg.daily.icc-rk") == std::string::npos)
        {
            return false;
        }

        if (!RewriteDailyLoginHttpRequestForTest(
                "GET http://www.daily.icc-rk/index.app HTTP/1.1\r\nHost: www.daily.icc-rk\r\n\r\n",
                rewritten) ||
            rewritten.find("http://uwg.daily.icc-rk/index.app") == std::string::npos ||
            rewritten.find("Host: uwg.daily.icc-rk") == std::string::npos)
        {
            return false;
        }

        if (RewriteDailyLoginHttpRequestForTest(
                "GET /market/ HTTP/1.1\r\nHost: aomarket.funcom.com\r\n\r\n",
                rewritten))
        {
            return false;
        }

        return true;
    }
}
