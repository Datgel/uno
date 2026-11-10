// Launches the repro app N times. In each launch it changes the ComboBox selection twice with the keyboard (Down) and
// reads the combo's SelectionPattern through IUIAutomation: once at +1.5 s, then every 250 ms until it names the
// expected item (or 10 s pass).
//
// Usage: UiaClient <path to ComboUiaRepro.App.exe> [launches=10] [--structure-events]
//   --structure-events  also subscribe to StructureChanged (TreeScope_Subtree) on the app window, as many UIA clients do.
using System.Diagnostics;
using System.Runtime.InteropServices;
using Interop.UIAutomationClient;

const int UIA_SelectionPatternId = 10001;
const int UIA_ProcessIdPropertyId = 30002;
const int UIA_AutomationIdPropertyId = 30011;

if (args.Length < 1)
{
    Console.WriteLine("Usage: UiaClient <ComboUiaRepro.App.exe> [launches=10] [--structure-events]");
    return 2;
}

var appPath = args[0];
var launches = args.Length > 1 && int.TryParse(args[1], out var n) ? n : 10;
var structureEvents = args.Contains("--structure-events");

var uia = new CUIAutomation();
var firstEmpty = 0;
var firstLate = new List<long>();
var laterEmpty = 0;

for (var launch = 1; launch <= launches; launch++)
{
    using var app = Process.Start(new ProcessStartInfo(appPath) { UseShellExecute = false })!;
    try
    {
        var window = WaitFor(() => uia.GetRootElement().FindFirst(TreeScope.TreeScope_Children,
            uia.CreatePropertyCondition(UIA_ProcessIdPropertyId, app.Id)), TimeSpan.FromSeconds(60), "app window");
        var combo = WaitFor(() => window.FindFirst(TreeScope.TreeScope_Descendants,
            uia.CreatePropertyCondition(UIA_AutomationIdPropertyId, "Combo")), TimeSpan.FromSeconds(30), "combo");

        StructureHandler? handler = null;
        if (structureEvents)
        {
            handler = new StructureHandler();
            uia.AddStructureChangedEventHandler(window, TreeScope.TreeScope_Subtree, null, handler);
        }

        Thread.Sleep(3000); // let the launch settle
        Console.WriteLine($"launch {launch}: before any change the selection reads [{string.Join(", ", ReadSelection(combo))}]");

        for (var change = 1; change <= 2; change++)
        {
            var expected = $"Item {change}";
            PressDown(combo);
            var clock = Stopwatch.StartNew();
            Thread.Sleep(1500);
            var atFirstRead = ReadSelection(combo);
            var readable = atFirstRead.Count == 1 && atFirstRead[0] == expected;
            while (!readable && clock.ElapsedMilliseconds < 10_000)
            {
                Thread.Sleep(250);
                var now = ReadSelection(combo);
                readable = now.Count == 1 && now[0] == expected;
            }

            var firstReadText = atFirstRead.Count == 0 ? "EMPTY" : string.Join(", ", atFirstRead);
            var result = readable ? $"reads [{expected}] at +{clock.ElapsedMilliseconds} ms" : "still not readable after 10 s";
            Console.WriteLine($"launch {launch} change {change}: at +1.5 s the selection read [{firstReadText}]; {result}");

            if (atFirstRead.Count == 0)
            {
                if (change == 1) { firstEmpty++; firstLate.Add(readable ? clock.ElapsedMilliseconds : -1); }
                else laterEmpty++;
            }
        }

        if (handler is not null)
            uia.RemoveStructureChangedEventHandler(window, handler);
    }
    finally
    {
        if (!app.HasExited) app.Kill();
        app.WaitForExit(10_000);
    }

    Thread.Sleep(2000);
}

Console.WriteLine();
Console.WriteLine($"First change per launch: EMPTY at +1.5 s in {firstEmpty} of {launches} launches" +
    (firstLate.Count > 0 ? $"; readable at +{string.Join(", +", firstLate.Select(ms => ms < 0 ? "never" : ms + " ms"))}" : ""));
Console.WriteLine($"Second change per launch: EMPTY at +1.5 s in {laterEmpty} of {launches} launches");
return 0;

static List<string> ReadSelection(IUIAutomationElement combo)
{
    var names = new List<string>();
    if (combo.GetCurrentPattern(UIA_SelectionPatternId) is not IUIAutomationSelectionPattern selection) return names;
    var array = selection.GetCurrentSelection();
    for (var i = 0; array is not null && i < array.Length; i++)
        names.Add(array.GetElement(i).CurrentName);
    return names;
}

static void PressDown(IUIAutomationElement combo)
{
    combo.SetFocus();
    Thread.Sleep(300);
    const ushort VK_DOWN = 0x28;
    var inputs = new[]
    {
        Native.KeyInput(VK_DOWN, keyUp: false),
        Native.KeyInput(VK_DOWN, keyUp: true),
    };
    Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Native.INPUT>());
}

static IUIAutomationElement WaitFor(Func<IUIAutomationElement?> find, TimeSpan timeout, string what)
{
    var clock = Stopwatch.StartNew();
    while (clock.Elapsed < timeout)
    {
        if (find() is { } found) return found;
        Thread.Sleep(250);
    }

    throw new TimeoutException($"The {what} did not appear within {timeout.TotalSeconds:F0} s.");
}

sealed class StructureHandler : IUIAutomationStructureChangedEventHandler
{
    public void HandleStructureChangedEvent(IUIAutomationElement sender, StructureChangeType changeType, int[] runtimeId) { }
}

static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public InputUnion u; }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

    public static INPUT KeyInput(ushort vk, bool keyUp) => new()
    {
        type = 1, // INPUT_KEYBOARD
        u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = keyUp ? 0x0002u : 0u } }, // KEYEVENTF_KEYUP
    };

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
}
