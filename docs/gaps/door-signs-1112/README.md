# Arrival Hall door signs (307000 / 307001 / 307002) — staged, not deployed

Kit path (authoritative binaries and apply tool):

  I:\AO\staging\door-signs-1112

Do not copy this kit into I:\AO\client-ddogs, I:\AO\src\AORebirth\GameData, or the launcher dist.
Live PF 4604 boards must stay on stock 295853 / 304368 / 295852 until the client patch is in the launcher.

## Mappings

| Dynel        | Offset | Was                | Becomes                      |
|--------------|--------|--------------------|------------------------------|
| 0xC00B11FC   | 1325   | 295853 Clans       | 307000 Arete                 |
| 0xC00C11FC   | 1413   | 304368 Neutral     | 307001 ICC Shuttleport       |
| 0xC00D11FC   | 1501   | 295852 Omni-Tek    | 307002 Jobe                  |

307000 keeps the 2026-11-11 photographic Arete bake. 307001 / 307002 use generated plaques
(concrete from the Arete wall photo, Eurostile-like Saira Extra Expanded caps, neon green / light blue).
A generated ARETE preview (warm #FFD2B0 core, red glow) is in the kit art folder as an alternate only.

## Apply (RC test only)

  I:\AO\staging\door-signs-1112\tools\ApplyDoorSigns\ApplyDoorSigns.exe apply --client "<RC test client>" --records "I:\AO\staging\door-signs-1112\client-rdb"

The tool refuses I:\AO\client-ddogs. Default UV bake is --flip hv.

Staged server files (RC GameData only):

  I:\AO\staging\door-signs-1112\server\Playfields\4604\Dynels.dat
  I:\AO\staging\door-signs-1112\server\items.dat   (131810 templates; stock 131807 plus the three new items)

Art previews also live on the box at /workspace/door-signs/ and under I:\AO\imports\door-signs-1112.
