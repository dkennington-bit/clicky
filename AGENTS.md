# Clicky - Agent Instructions

<!-- This is the single source of truth for all AI coding agents. CLAUDE.md is a symlink to this file. -->
<!-- AGENTS.md spec: https://github.com/agentsmd/agents.md — supported by Claude Code, Cursor, Copilot, Gemini CLI, and others. -->

## Overview

Clicky has two side-by-side desktop implementations:

- **macOS app**: menu bar companion app. Lives entirely in the macOS status bar (no dock icon, no main window). Clicking the menu bar icon opens a custom floating panel with companion voice controls. Uses push-to-talk (ctrl+option) to capture voice input, transcribes it via AssemblyAI streaming, and sends the transcript + a screenshot of the user's screen to Claude. Claude responds with text (streamed via SSE) and voice (ElevenLabs TTS). A blue cursor overlay can fly to and point at UI elements Claude references on any connected monitor.
- **Windows app**: native WPF tray companion under `windows/Clicky.Windows`. Uses Ctrl+Alt push-to-talk, OpenAI transcription, OpenAI `gpt-5.5` vision/chat through the Responses API, OpenAI `gpt-4o-mini-tts`, Windows Credential Manager for the user's OpenAI key, and transparent topmost WPF overlay windows for the blue cursor.

macOS API keys live on a Cloudflare Worker proxy. The Windows rewrite uses a user-provided OpenAI key stored locally in Windows Credential Manager under `Clicky.OpenAI.ApiKey`.

## Architecture

- **App Type**: Menu bar-only (`LSUIElement=true`), no dock icon or main window
- **Framework**: SwiftUI (macOS native) with AppKit bridging for menu bar panel and cursor overlay
- **Pattern**: MVVM with `@StateObject` / `@Published` state management
- **AI Chat**: Claude (Sonnet 4.6 default, Opus 4.6 optional) via Cloudflare Worker proxy with SSE streaming
- **Speech-to-Text**: AssemblyAI real-time streaming (`u3-rt-pro` model) via websocket, with OpenAI and Apple Speech as fallbacks
- **Text-to-Speech**: ElevenLabs (`eleven_flash_v2_5` model) via Cloudflare Worker proxy
- **Screen Capture**: ScreenCaptureKit (macOS 14.2+), multi-monitor support
- **Voice Input**: Push-to-talk via `AVAudioEngine` + pluggable transcription-provider layer. System-wide keyboard shortcut via listen-only CGEvent tap.
- **Element Pointing**: Claude embeds `[POINT:x,y:label:screenN]` tags in responses. The overlay parses these, maps coordinates to the correct monitor, and animates the blue cursor along a bezier arc to the target.
- **Concurrency**: `@MainActor` isolation, async/await throughout
- **Analytics**: PostHog via `ClickyAnalytics.swift`

### Windows Architecture

- **App Type**: Windows notification-area tray app with no main window
- **Framework**: .NET 8 WPF with Windows Forms `NotifyIcon` for the tray
- **AI Chat**: OpenAI Responses API using `gpt-5.5` with streamed SSE text and screenshot image inputs
- **Speech-to-Text**: OpenAI Audio Transcriptions using `gpt-4o-mini-transcribe`
- **Text-to-Speech**: OpenAI Audio Speech using `gpt-4o-mini-tts` with default voice `alloy`, played with NAudio
- **Screen Capture**: GDI screen capture across all connected Windows displays
- **Voice Input**: Ctrl+Alt push-to-talk with a low-level keyboard hook and microphone capture via NAudio
- **Element Pointing**: GPT appends `[POINT:x,y:label:screenN]` tags. The Windows overlay maps screenshot pixels back to monitor pixels and animates the blue cursor.
- **API Keys**: User enters an OpenAI API key in the panel. The app stores it in Windows Credential Manager and never logs it.
- **Logging**: Windows runtime logs are written to `%APPDATA%\Clicky.Windows\logs\current.log`. Logs include statuses, transcripts, assistant responses, HTTP status codes, and exception stack traces, but not API keys or screenshots.

### API Proxy (Cloudflare Worker)

The app never calls external APIs directly. All requests go through a Cloudflare Worker (`worker/src/index.ts`) that holds the real API keys as secrets.

| Route | Upstream | Purpose |
|-------|----------|---------|
| `POST /chat` | `api.anthropic.com/v1/messages` | Claude vision + streaming chat |
| `POST /tts` | `api.elevenlabs.io/v1/text-to-speech/{voiceId}` | ElevenLabs TTS audio |
| `POST /transcribe-token` | `streaming.assemblyai.com/v3/token` | Fetches a short-lived (480s) AssemblyAI websocket token |

Worker secrets: `ANTHROPIC_API_KEY`, `ASSEMBLYAI_API_KEY`, `ELEVENLABS_API_KEY`
Worker vars: `ELEVENLABS_VOICE_ID`

### Key Architecture Decisions

**Menu Bar Panel Pattern**: The companion panel uses `NSStatusItem` for the menu bar icon and a custom borderless `NSPanel` for the floating control panel. This gives full control over appearance (dark, rounded corners, custom shadow) and avoids the standard macOS menu/popover chrome. The panel is non-activating so it doesn't steal focus. A global event monitor auto-dismisses it on outside clicks.

**Cursor Overlay**: A full-screen transparent `NSPanel` hosts the blue cursor companion. It's non-activating, joins all Spaces, and never steals focus. The cursor position, response text, waveform, and pointing animations all render in this overlay via SwiftUI through `NSHostingView`.

**Global Push-To-Talk Shortcut**: Background push-to-talk uses a listen-only `CGEvent` tap instead of an AppKit global monitor so modifier-based shortcuts like `ctrl + option` are detected more reliably while the app is running in the background.

**Shared URLSession for AssemblyAI**: A single long-lived `URLSession` is shared across all AssemblyAI streaming sessions (owned by the provider, not the session). Creating and invalidating a URLSession per session corrupts the OS connection pool and causes "Socket is not connected" errors after a few rapid reconnections.

**Transient Cursor Mode**: When "Show Clicky" is off, pressing the hotkey fades in the cursor overlay for the duration of the interaction (recording → response → TTS → optional pointing), then fades it out automatically after 1 second of inactivity.

## Key Files

| File | Lines | Purpose |
|------|-------|---------|
| `leanring_buddyApp.swift` | ~89 | Menu bar app entry point. Uses `@NSApplicationDelegateAdaptor` with `CompanionAppDelegate` which creates `MenuBarPanelManager` and starts `CompanionManager`. No main window — the app lives entirely in the status bar. |
| `CompanionManager.swift` | ~1026 | Central state machine. Owns dictation, shortcut monitoring, screen capture, Claude API, ElevenLabs TTS, and overlay management. Tracks voice state (idle/listening/processing/responding), conversation history, model selection, and cursor visibility. Coordinates the full push-to-talk → screenshot → Claude → TTS → pointing pipeline. |
| `MenuBarPanelManager.swift` | ~243 | NSStatusItem + custom NSPanel lifecycle. Creates the menu bar icon, manages the floating companion panel (show/hide/position), installs click-outside-to-dismiss monitor. |
| `CompanionPanelView.swift` | ~761 | SwiftUI panel content for the menu bar dropdown. Shows companion status, push-to-talk instructions, model picker (Sonnet/Opus), permissions UI, DM feedback button, and quit button. Dark aesthetic using `DS` design system. |
| `OverlayWindow.swift` | ~881 | Full-screen transparent overlay hosting the blue cursor, response text, waveform, and spinner. Handles cursor animation, element pointing with bezier arcs, multi-monitor coordinate mapping, and fade-out transitions. |
| `CompanionResponseOverlay.swift` | ~217 | SwiftUI view for the response text bubble and waveform displayed next to the cursor in the overlay. |
| `CompanionScreenCaptureUtility.swift` | ~132 | Multi-monitor screenshot capture using ScreenCaptureKit. Returns labeled image data for each connected display. |
| `BuddyDictationManager.swift` | ~866 | Push-to-talk voice pipeline. Handles microphone capture via `AVAudioEngine`, provider-aware permission checks, keyboard/button dictation sessions, transcript finalization, shortcut parsing, contextual keyterms, and live audio-level reporting for waveform feedback. |
| `BuddyTranscriptionProvider.swift` | ~100 | Protocol surface and provider factory for voice transcription backends. Resolves provider based on `VoiceTranscriptionProvider` in Info.plist — AssemblyAI, OpenAI, or Apple Speech. |
| `AssemblyAIStreamingTranscriptionProvider.swift` | ~478 | Streaming transcription provider. Fetches temp tokens from the Cloudflare Worker, opens an AssemblyAI v3 websocket, streams PCM16 audio, tracks turn-based transcripts, and delivers finalized text on key-up. Shares a single URLSession across all sessions. |
| `OpenAIAudioTranscriptionProvider.swift` | ~317 | Upload-based transcription provider. Buffers push-to-talk audio locally, uploads as WAV on release, returns finalized transcript. |
| `AppleSpeechTranscriptionProvider.swift` | ~147 | Local fallback transcription provider backed by Apple's Speech framework. |
| `BuddyAudioConversionSupport.swift` | ~108 | Audio conversion helpers. Converts live mic buffers to PCM16 mono audio and builds WAV payloads for upload-based providers. |
| `GlobalPushToTalkShortcutMonitor.swift` | ~132 | System-wide push-to-talk monitor. Owns the listen-only `CGEvent` tap and publishes press/release transitions. |
| `ClaudeAPI.swift` | ~291 | Claude vision API client with streaming (SSE) and non-streaming modes. TLS warmup optimization, image MIME detection, conversation history support. |
| `OpenAIAPI.swift` | ~142 | OpenAI GPT vision API client. |
| `ElevenLabsTTSClient.swift` | ~81 | ElevenLabs TTS client. Sends text to the Worker proxy, plays back audio via `AVAudioPlayer`. Exposes `isPlaying` for transient cursor scheduling. |
| `ElementLocationDetector.swift` | ~335 | Detects UI element locations in screenshots for cursor pointing. |
| `DesignSystem.swift` | ~880 | Design system tokens — colors, corner radii, shared styles. All UI references `DS.Colors`, `DS.CornerRadius`, etc. |
| `ClickyAnalytics.swift` | ~121 | PostHog analytics integration for usage tracking. |
| `WindowPositionManager.swift` | ~262 | Window placement logic, Screen Recording permission flow, and accessibility permission helpers. |
| `AppBundleConfiguration.swift` | ~28 | Runtime configuration reader for keys stored in the app bundle Info.plist. |
| `worker/src/index.ts` | ~142 | Cloudflare Worker proxy. Three routes: `/chat` (Claude), `/tts` (ElevenLabs), `/transcribe-token` (AssemblyAI temp token). |
| `windows/Clicky.Windows/src/Clicky.Windows/Infrastructure/ClickyApplicationCoordinator.cs` | ~379 | Windows app coordinator. Wires tray, panel, overlay, Ctrl+Alt hotkey, microphone recording, screen capture, OpenAI clients, TTS, and conversation state. |
| `windows/Clicky.Windows/src/Clicky.Windows/UI/CompanionPanelWindow.cs` | ~244 | Borderless WPF tray panel for status, OpenAI key setup, push-to-talk instructions, cursor toggle, transcript, response, and quit. |
| `windows/Clicky.Windows/src/Clicky.Windows/UI/OverlayWindow.cs` | ~301 | Transparent topmost WPF overlay that renders the blue cursor, waveform, spinner, response bubble, and pointing animation. |
| `windows/Clicky.Windows/src/Clicky.Windows/UI/OverlayWindowManager.cs` | ~174 | Creates one Windows overlay per monitor, hides overlays during screenshot capture, broadcasts voice state, and routes point tags to the correct screen. |
| `windows/Clicky.Windows/src/Clicky.Windows/AI/OpenAIConversationClient.cs` | ~129 | Streams OpenAI Responses API output from `gpt-5.5`, parses SSE text deltas, and separates `[POINT:...]` metadata from spoken text. |
| `windows/Clicky.Windows/src/Clicky.Windows/AI/OpenAITranscriptionClient.cs` | ~53 | Uploads push-to-talk WAV audio to OpenAI `gpt-4o-mini-transcribe`. |
| `windows/Clicky.Windows/src/Clicky.Windows/AI/OpenAISpeechClient.cs` | ~115 | Sends text to OpenAI `gpt-4o-mini-tts` and plays WAV audio with NAudio. |
| `windows/Clicky.Windows/src/Clicky.Windows/AI/OpenAIRequestFactory.cs` | ~153 | Builds testable OpenAI request payloads for Responses, transcription, and speech endpoints. |
| `windows/Clicky.Windows/src/Clicky.Windows/AI/PointTagParser.cs` | ~61 | Parses and strips `[POINT:none]`, `[POINT:x,y:label]`, and `[POINT:x,y:label:screenN]` tags. |
| `windows/Clicky.Windows/src/Clicky.Windows/Audio/MicrophonePushToTalkRecorder.cs` | ~152 | Records microphone audio as WAV while Ctrl+Alt is held and publishes normalized audio levels for the waveform. |
| `windows/Clicky.Windows/src/Clicky.Windows/Input/GlobalPushToTalkHotkeyListener.cs` | ~133 | Installs the low-level Windows keyboard hook for Ctrl+Alt press/release transitions. |
| `windows/Clicky.Windows/src/Clicky.Windows/ScreenCapture/WindowsScreenCaptureService.cs` | ~70 | Captures JPEG screenshots for all connected Windows displays and labels the cursor screen as primary focus. |
| `windows/Clicky.Windows/src/Clicky.Windows/Security/CredentialManagerOpenAIApiKeyStore.cs` | ~117 | Stores, reads, and deletes the OpenAI key via Windows Credential Manager. |
| `windows/Clicky.Windows/src/Clicky.Windows/Logging/ClickyLogger.cs` | ~52 | Writes rotating runtime logs to `%APPDATA%\Clicky.Windows\logs\current.log` for debugging local interaction failures. |
| `windows/Clicky.Windows/tests/Clicky.Windows.Tests/*.cs` | ~230 | Unit tests for point-tag parsing, coordinate mapping, OpenAI payload construction, and API-key store contract behavior. |

## Build & Run

```bash
# Open in Xcode
open leanring-buddy.xcodeproj

# Select the leanring-buddy scheme, set signing team, Cmd+R to build and run

# Known non-blocking warnings: Swift 6 concurrency warnings,
# deprecated onChange warning in OverlayWindow.swift. Do NOT attempt to fix these.
```

**Do NOT run `xcodebuild` from the terminal** — it invalidates TCC (Transparency, Consent, and Control) permissions and the app will need to re-request screen recording, accessibility, etc.

### Windows

```powershell
cd windows\Clicky.Windows
dotnet restore
dotnet build
dotnet run --project src\Clicky.Windows\Clicky.Windows.csproj
dotnet test tests\Clicky.Windows.Tests\Clicky.Windows.Tests.csproj
```

Windows requires the .NET 8 SDK and an OpenAI API key entered through the Clicky tray panel. The key is stored in Windows Credential Manager as `Clicky.OpenAI.ApiKey`.

## Cloudflare Worker

```bash
cd worker
npm install

# Add secrets
npx wrangler secret put ANTHROPIC_API_KEY
npx wrangler secret put ASSEMBLYAI_API_KEY
npx wrangler secret put ELEVENLABS_API_KEY

# Deploy
npx wrangler deploy

# Local dev (create worker/.dev.vars with your keys)
npx wrangler dev
```

## Code Style & Conventions

### Variable and Method Naming

IMPORTANT: Follow these naming rules strictly. Clarity is the top priority.

- Be as clear and specific with variable and method names as possible
- **Optimize for clarity over concision.** A developer with zero context on the codebase should immediately understand what a variable or method does just from reading its name
- Use longer names when it improves clarity. Do NOT use single-character variable names
- Example: use `originalQuestionLastAnsweredDate` instead of `originalAnswered`
- When passing props or arguments to functions, keep the same names as the original variable. Do not shorten or abbreviate parameter names. If you have `currentCardData`, pass it as `currentCardData`, not `card` or `cardData`

### Code Clarity

- **Clear is better than clever.** Do not write functionality in fewer lines if it makes the code harder to understand
- Write more lines of code if additional lines improve readability and comprehension
- Make things so clear that someone with zero context would completely understand the variable names, method names, what things do, and why they exist
- When a variable or method name alone cannot fully explain something, add a comment explaining what is happening and why

### Swift/SwiftUI Conventions

- Use SwiftUI for all UI unless a feature is only supported in AppKit (e.g., `NSPanel` for floating windows)
- All UI state updates must be on `@MainActor`
- Use async/await for all asynchronous operations
- Comments should explain "why" not just "what", especially for non-obvious AppKit bridging
- AppKit `NSPanel`/`NSWindow` bridged into SwiftUI via `NSHostingView`
- All buttons must show a pointer cursor on hover
- For any interactive element, explicitly think through its hover behavior (cursor, visual feedback, and whether hover should communicate clickability)

### Do NOT

- Do not add features, refactor code, or make "improvements" beyond what was asked
- Do not add docstrings, comments, or type annotations to code you did not change
- Do not try to fix the known non-blocking warnings (Swift 6 concurrency, deprecated onChange)
- Do not rename the project directory or scheme (the "leanring" typo is intentional/legacy)
- Do not run `xcodebuild` from the terminal — it invalidates TCC permissions

## Git Workflow

- Branch naming: `feature/description` or `fix/description`
- Commit messages: imperative mood, concise, explain the "why" not the "what"
- For Windows app code updates, run `dotnet build` from `windows/Clicky.Windows` before committing, and start the commit message with `Codex5.5`.
- Do not force-push to main

## Self-Update Instructions

<!-- AI agents: follow these instructions to keep this file accurate. -->

When you make changes to this project that affect the information in this file, update this file to reflect those changes. Specifically:

1. **New files**: Add new source files to the "Key Files" table with their purpose and approximate line count
2. **Deleted files**: Remove entries for files that no longer exist
3. **Architecture changes**: Update the architecture section if you introduce new patterns, frameworks, or significant structural changes
4. **Build changes**: Update build commands if the build process changes
5. **New conventions**: If the user establishes a new coding convention during a session, add it to the appropriate conventions section
6. **Line count drift**: If a file's line count changes significantly (>50 lines), update the approximate count in the Key Files table

Do NOT update this file for minor edits, bug fixes, or changes that don't affect the documented architecture or conventions.
