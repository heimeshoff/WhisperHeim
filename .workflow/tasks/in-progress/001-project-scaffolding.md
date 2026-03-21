# Task: Project Scaffolding

**ID:** 001
**Milestone:** M1 - Live Dictation + Core App
**Size:** Small
**Created:** 2026-03-21
**Dependencies:** None

## Objective
Create the .NET solution with a WPF project configured with all core NuGet packages.

## Details
Create a .NET 9 solution `WhisperHeim.sln` with a WPF app project `WhisperHeim`. Add NuGet packages: WPF-UI, WPF-UI.Tray, NAudio, org.k2fsa.sherpa.onnx, Microsoft.ML.OnnxRuntime. Set up basic App.xaml with ShutdownMode="OnExplicitShutdown". Configure for x64 only (ONNX models are 64-bit). Add .gitignore for .NET/WPF.

## Acceptance Criteria
- [ ] Solution builds
- [ ] App launches and exits cleanly
- [ ] All NuGet packages resolve

## Notes
.NET 9, x64 only. ShutdownMode="OnExplicitShutdown" is required for tray icon lifecycle.

## Work Log
<!-- Appended by /work during execution -->
