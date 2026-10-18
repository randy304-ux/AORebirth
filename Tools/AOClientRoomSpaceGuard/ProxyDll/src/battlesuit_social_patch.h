#pragma once

#include <cstddef>
#include <cstdint>

namespace aorf
{
    // Lets battle suits (back-slot body-swap armor flagged as social armor) be worn in the
    // armor page with other armor. Gamecode.dll's social-armor check returns "allowed" for
    // the battle suit item ids only; every other social item keeps the stock rule.
    // Returns false when Gamecode.dll is not loaded or the check is not found exactly once.
    bool InstallBattlesuitSocialPatch();

    bool RunBattlesuitSocialPatchSelfTest();
}