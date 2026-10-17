#pragma once

namespace aorf
{
    // Loads ao-vk's AOReloaded build from aovk-reloaded.dll when the launcher
    // has installed it beside AnarchyOnline.exe. Our version.dll stays the one
    // Windows loaded, so the server and Store redirects keep running.
    bool LoadAovkReloaded();
}
