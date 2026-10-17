#pragma once

#include <cstddef>
#include <cstdint>

namespace aorf
{
    // Lets the in-game browser open http(s) links with ShellExecute.
    // Returns false when GUI.dll is not loaded yet or this client build
    // does not contain the one known gate.
    bool InstallExternalLinkPatch();

    bool RunExternalLinkPatchSelfTest();

    // Test hook. Patches a memory image that contains the gate exactly once.
    bool PatchExternalLinkGateForTest(uint8_t* image, size_t size);
}
