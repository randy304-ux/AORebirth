namespace ZoneEngine_New.Core.Playfield;

using System;
using System.Collections.Generic;
using AORebirth.Core.Vector;

using ZoneEngine_New.Core.Entities;
using ZoneEngine_New.Core.Logging;

/// <summary>Runs one named spawn group at a time on this playfield, with timed stages and a reset.</summary>
public sealed class SpawnGroupService
{
    readonly Playfield _playfield;
    readonly GameData.IGameData _gameData;
    readonly IZoneLogger _logger;
    readonly object _sync = new();
    RunningGroup? _running;

    public SpawnGroupService(Playfield playfield, GameData.IGameData gameData, IZoneLogger logger)
    {
        _playfield = playfield;
        _gameData = gameData;
        _logger = logger;
    }

    public bool IsRunning(string groupId)
    {
        lock (_sync)
            return _running != null
                && string.Equals(_running.Definition.Id, groupId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Starts <paramref name="groupId"/> if it belongs on this playfield and is not already running.</summary>
    public bool TryStart(string groupId)
    {
        if (!_gameData.SpawnGroups.TryGet(groupId, out SpawnGroupDefinition? group) || group == null)
            return false;
        if (group.Playfield != 0 && group.Playfield != _playfield.Identity.Instance)
            return false;

        lock (_sync)
        {
            if (_running != null)
                return false;

            _running = new RunningGroup(group);
        }

        SpawnDueStages(0);
        return true;
    }

    public void Tick(double deltaTime)
    {
        MaybeAutoStart();

        RunningGroup? running;
        lock (_sync)
            running = _running;
        if (running == null)
            return;

        running.Elapsed += deltaTime;
        SpawnDueStages(running.Elapsed);
        Evaluate(running);
    }

    void SpawnDueStages(double elapsed)
    {
        RunningGroup? running;
        lock (_sync)
            running = _running;
        if (running == null)
            return;

        SpawnService? spawns = _playfield.GetService<SpawnService>();
        if (spawns == null)
            return;

        for (int i = 0; i < running.Definition.Stages.Length; i++)
        {
            if (running.SpawnedStages.Contains(i))
                continue;
            SpawnGroupStage stage = running.Definition.Stages[i];
            if (elapsed + 0.001 < stage.DelaySeconds)
                continue;
            if (stage.WaitUntilAllDead is { Length: > 0 } waitHashes && !AllDeadOnPlayfield(waitHashes))
                continue;

            running.SpawnedStages.Add(i);
            if (stage.Spawns == null)
                continue;

            foreach (SpawnGroupEntry entry in stage.Spawns)
            {
                if (string.IsNullOrWhiteSpace(entry.HashText) || entry.Position == null || entry.Position.Length < 3)
                    continue;
                try
                {
                    var at = new Vector3(entry.Position[0], entry.Position[1], entry.Position[2]);
                    Quaternion heading = SpawnHeading.FromFacingDegrees(entry.Angle);
                    NpcCharacter npc = spawns.Spawn(entry.HashText, at, heading, entry.MinLevel);
                    running.Live.Add(npc);
                    running.SpawnedHashes.Add(entry.HashText);
                }
                catch (Exception ex)
                {
                    _logger.Warn("Spawn group " + running.Definition.Id + " failed to spawn " + entry.HashText + ": " + ex.Message);
                }
            }
        }
    }

    void Evaluate(RunningGroup running)
    {
        PruneDead(running);

        if (HasFailed(running) || running.Elapsed >= Math.Max(1, running.Definition.ResetTimeoutSeconds))
        {
            Reset("fail");
            return;
        }

        if (HasSucceeded(running))
        {
            if (running.SuccessAt < 0)
                running.SuccessAt = running.Elapsed;
            if (running.Elapsed - running.SuccessAt >= Math.Max(0, running.Definition.SuccessDespawnSeconds))
                Reset("success");
        }
    }

    static void PruneDead(RunningGroup running)
    {
        for (int i = running.Live.Count - 1; i >= 0; i--)
        {
            NpcCharacter npc = running.Live[i];
            if (IsAlive(npc))
                continue;
            if (!string.IsNullOrEmpty(npc.SpawnHash))
                running.DeadHashes.Add(npc.SpawnHash);
            running.Live.RemoveAt(i);
        }
    }

    // Death goes through SpawnService.DespawnNpc, which clears Playfield.
    static bool IsAlive(NpcCharacter npc) => npc.Playfield != null && !npc.IsDead;

    static bool HasFailed(RunningGroup running)
    {
        foreach (string hash in running.Definition.FailIfDead ?? Array.Empty<string>())
        {
            if (running.SpawnedHashes.Contains(hash) && running.DeadHashes.Contains(hash) && !AnyLive(running, hash))
                return true;
        }

        foreach (string[] row in running.Definition.FailIfAllDead ?? Array.Empty<string[]>())
        {
            if (row == null || row.Length == 0)
                continue;
            bool allDead = true;
            bool anySpawned = false;
            foreach (string hash in row)
            {
                if (!running.SpawnedHashes.Contains(hash))
                {
                    allDead = false;
                    break;
                }
                anySpawned = true;
                if (AnyLive(running, hash))
                {
                    allDead = false;
                    break;
                }
            }
            if (anySpawned && allDead)
                return true;
        }

        return false;
    }

    static bool HasSucceeded(RunningGroup running)
    {
        string[] need = running.Definition.SuccessIfDead ?? Array.Empty<string>();
        if (need.Length == 0)
            return false;
        foreach (string hash in need)
        {
            if (!running.SpawnedHashes.Contains(hash) || !running.DeadHashes.Contains(hash) || AnyLive(running, hash))
                return false;
        }
        return true;
    }

    static bool AnyLive(RunningGroup running, string hash)
    {
        foreach (NpcCharacter npc in running.Live)
        {
            if (IsAlive(npc) && string.Equals(npc.SpawnHash, hash, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }


    void MaybeAutoStart()
    {
        lock (_sync)
        {
            if (_running != null)
                return;
        }

        foreach (SpawnGroupDefinition group in _gameData.SpawnGroups.All)
        {
            if (group == null || !group.AutoStart)
                continue;
            if (group.Playfield != 0 && group.Playfield != _playfield.Identity.Instance)
                continue;

            // Re-arm only when any WaitUntilAllDead hashes on the first stage are currently alive,
            // so bosses do not immediately respawn while the standing towers are still down.
            SpawnGroupStage? first = group.Stages is { Length: > 0 } ? group.Stages[0] : null;
            if (first?.WaitUntilAllDead is { Length: > 0 } hashes && !AllAliveOnPlayfield(hashes))
                continue;

            if (TryStart(group.Id))
                return;
        }
    }

    bool AllDeadOnPlayfield(string[] hashes)
    {
        HashSet<string> live = LiveHashesOnPlayfield();
        foreach (string hash in hashes)
        {
            if (live.Contains(hash))
                return false;
        }
        return hashes.Length > 0;
    }

    bool AllAliveOnPlayfield(string[] hashes)
    {
        HashSet<string> live = LiveHashesOnPlayfield();
        foreach (string hash in hashes)
        {
            if (!live.Contains(hash))
                return false;
        }
        return hashes.Length > 0;
    }

    HashSet<string> LiveHashesOnPlayfield()
    {
        var live = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        DynelRegistry? registry = _playfield.GetService<DynelRegistry>();
        if (registry == null)
            return live;
        foreach (Dynel dynel in registry.Dynels())
        {
            if (dynel is NpcCharacter npc && IsAlive(npc) && !string.IsNullOrEmpty(npc.SpawnHash))
                live.Add(npc.SpawnHash);
        }
        return live;
    }

    void Reset(string reason)
    {
        RunningGroup? running;
        lock (_sync)
        {
            running = _running;
            _running = null;
        }
        if (running == null)
            return;

        SpawnService? spawns = _playfield.GetService<SpawnService>();
        foreach (NpcCharacter npc in running.Live)
        {
            if (!IsAlive(npc) || spawns == null)
                continue;
            try
            {
                spawns.DespawnNpc(npc);
            }
            catch (Exception ex)
            {
                _logger.Warn("Spawn group " + running.Definition.Id + " despawn failed: " + ex.Message);
            }
        }

        _logger.Info("Spawn group " + running.Definition.Id + " reset (" + reason + ")");
    }

    sealed class RunningGroup
    {
        public RunningGroup(SpawnGroupDefinition definition)
        {
            Definition = definition;
        }

        public SpawnGroupDefinition Definition { get; }
        public double Elapsed { get; set; }
        public double SuccessAt { get; set; } = -1;
        public HashSet<int> SpawnedStages { get; } = new();
        public List<NpcCharacter> Live { get; } = new();
        public HashSet<string> SpawnedHashes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> DeadHashes { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>Playfield tick hook for <see cref="SpawnGroupService"/>.</summary>
public static class SpawnGroupClock
{
    public static void Tick(Playfield playfield, double deltaTime)
        => playfield.GetService<SpawnGroupService>()?.Tick(deltaTime);
}
