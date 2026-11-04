# Current Task

Deploy Randy-approved merc nested-text:// fix (raw NestedLink + nested filters).

Encoding rule: outer `href="text://"`; nested `href='text://'` with **raw** AOML
(no `&lt;` escape); nested bodies use unquoted attrs / `href=chatcmd://...`.
Sources: Nadybot/Tyrbot makeBlob; Athena/TSW scripting FAQ nested popups; InfoGuide.

Procedure: side-build + MercMenuCheck → local Mike commit → online=0 → DB backup →
shutdown-file stop → schtasks start → verify :7501/:7012/:7500. No push/PR.
