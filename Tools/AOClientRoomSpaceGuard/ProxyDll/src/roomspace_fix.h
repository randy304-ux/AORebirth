#pragma once

namespace aorf
{
    enum class ClientProfile
    {
        Unknown,
        NewClient,
        OldClient,
        FuncomCurrent
    };

    ClientProfile GetLoadedN3ClientProfile();
    const char* UnsupportedClientDetail();
    bool InstallClientCrashMitigation();
    bool RunClientCrashMitigationSelfTest();
}
