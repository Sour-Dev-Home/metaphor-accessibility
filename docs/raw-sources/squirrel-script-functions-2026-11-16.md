# The game's `Msg` and `Event` script functions, by name and address

- **Source:** a capture of one run of `scripts/deploy-dev.ps1` (Debug mod at commit `484013d` plus the registration
  listener committed with this file) on the maintainer's machine, through Reloaded-II 1.31.0. The development-only hook
  on `sq_newclosure` (`SquirrelRegistrationListener.cs`) logged each registration; these are the log's `register` lines
  whose name starts with `Msg` or `Event`, sorted. Method and results: `squirrel-registration-run-2026-11-16.md`.
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

Each line is a script name and the function's address relative to the game module. The run registered 1,514 names in
all; the rest are in the local log, not here.

```text
EventEnd 0x905440
EventFadeIn 0x905540
EventFadeOut 0x9055b0
EventFadeOut_Plain 0x9056a0
EventFadeSetPartsBehavior 0x905790
EventGuideClose 0x9057c0
EventLogAutoDisable 0x905950
EventLogAutoEnable 0x905970
EventMessageBgClose 0x905990
EventMoviePlay 0x905a90
EventStart 0x905cd0
EventUIBegin 0x905ce0
EventUIDisplaySuppressionEnd 0x905d00
EventUIDisplaySuppressionStart 0x905d10
EventUIEnd 0x905d20
EventWipeIsInEnd 0x905d30
EventWipeIsOutEnd 0x905d60
EventWipeIsOutPlayEnd 0x905d90
EventWipeStartIn 0x905dc0
EventWipeStartOut 0x905de0
MsgBacklogSeparator 0x90bd70
MsgBustupChangeFace 0x90bd80
MsgBustupSetCostume 0x90bda0
MsgBustupSetFace 0x90bdb0
MsgBustupSetOFF 0x90bdd0
MsgCanClose 0x90bde0
MsgCheckEnd 0x90bdf0
MsgCheckOpened 0x90be10
MsgCheckWindowClosed 0x90be20
MsgClearVariable 0x90be30
MsgClose 0x90be80
MsgEnd_KeepVoice 0x90be90
MsgGetHandleID 0x90bea0
MsgGetSelectNum 0x90bf70
MsgGetSelectedID 0x90bf80
MsgGreetingCheckEnd 0x90bf90
MsgGreetingClose 0x90bfb0
MsgGreetingRequest 0x90c050
MsgGreetingRequestID 0x90c090
MsgGreetingRequestID_Option 0x90c0e0
MsgGreetingRequest_Option 0x90c140
MsgIsSelectWindowOpened 0x90c1a0
MsgOpen 0x90c1b0
MsgReportClearRequested 0x90c270
MsgReportOpen 0x90c290
MsgReportOpenEx 0x90c300
MsgReportOpenID 0x90c380
MsgReportOpenIDEx 0x90c390
MsgReportOpenType 0x90c3a0
MsgReportOpenTypeID 0x90c410
MsgRequest 0x90c420
MsgRequestAccessSpot 0x90c460
MsgRequestAuto 0x90c4b0
MsgRequestID 0x90c4f0
MsgRequestIDAuto 0x90c530
MsgRequestNarration 0x90c580
MsgRequestSelect 0x90c5d0
MsgRequestSelectAccessSpot 0x90c620
MsgRequestSelectAccessSpot_Ex 0x90c670
MsgRequestSelectID 0x90c6c0
MsgRequestSelectID_Ex 0x90c6f0
MsgRequestSelect_CancelExit 0x90c720
MsgRequestSelect_Ex 0x90c770
MsgRequestSelect_Shop 0x90c7c0
MsgRequestSubtitles 0x90c820
MsgRequestSubtitlesAuto 0x90c860
MsgRequestSystemMessage 0x90c8b0
MsgRequestSystemMessageID 0x90c8f0
MsgRequestSystemMessageSelect 0x90c930
MsgRequestSystemMessageSelectID 0x90c980
MsgRequestSystemMessageSelectID_Ex 0x90c9b0
MsgRequestSystemMessageSelect_Ex 0x90c9e0
MsgSetSelectMask 0x90ca30
MsgSetSelectMaskID 0x90ca40
MsgSetSelectMaskLabel 0x90ca50
MsgSetVariable 0x90ca60
MsgSetVariableINT 0x90cc00
MsgSetVariableMapName 0x90cc60
MsgSystemConfirm 0x90cc70
MsgSystemConfirmCheckEnd 0x90ccb0
MsgSystemConfirmGetSelectedID 0x90ccd0
```
