using System;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
var path = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "WhisperHeim", "models", "silero-vad", "silero_vad.onnx");
Console.WriteLine($"Model: {path}");
using var session = new InferenceSession(path);
Console.WriteLine("INPUTS:");
foreach (var inp in session.InputMetadata)
    Console.WriteLine($"  {inp.Key}: [{string.Join(",", inp.Value.Dimensions)}] {inp.Value.ElementType}");
Console.WriteLine("OUTPUTS:");
foreach (var outp in session.OutputMetadata)
    Console.WriteLine($"  {outp.Key}: [{string.Join(",", outp.Value.Dimensions)}] {outp.Value.ElementType}");
