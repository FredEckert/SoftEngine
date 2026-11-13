using System.Runtime.InteropServices;

namespace SoftEngine.App;

/// <summary>
/// Thin P/Invoke layer over the SDL2 C API. Names, constants and struct offsets match
/// SDL2's headers exactly, so this reads like the C SDL2 code you already know.
/// Only the handful of calls this app needs are declared.
/// </summary>
internal static unsafe class SDL
{
    private const string Lib = "SDL2";

    /// <summary>Call once before any other SDL function. Finds SDL2 on Windows and Linux.</summary>
    public static void Setup()
    {
        NativeLibrary.SetDllImportResolver(typeof(SDL).Assembly, (name, assembly, path) =>
        {
            if (name != Lib) return IntPtr.Zero;
            // Windows: SDL2.dll. Debian runtime package (libsdl2-2.0-0): libSDL2-2.0.so.0.
            foreach (var candidate in new[] { "SDL2", "libSDL2-2.0.so.0", "libSDL2.so", "libSDL2-2.0.0.dylib" })
                if (NativeLibrary.TryLoad(candidate, assembly, path, out var handle))
                    return handle;
            return IntPtr.Zero;
        });
    }

    // ---- SDL_Init flags ----
    public const uint SDL_INIT_VIDEO = 0x00000020;
    public const uint SDL_INIT_JOYSTICK = 0x00000200;
    public const uint SDL_INIT_GAMECONTROLLER = 0x00002000;

    // ---- Window / renderer / texture ----
    public const int SDL_WINDOWPOS_CENTERED = 0x2FFF0000;
    public const uint SDL_WINDOW_SHOWN = 0x00000004;
    public const uint SDL_WINDOW_RESIZABLE = 0x00000020;
    public const uint SDL_RENDERER_ACCELERATED = 0x00000002;
    public const uint SDL_RENDERER_PRESENTVSYNC = 0x00000004;
    public const uint SDL_PIXELFORMAT_ARGB8888 = 0x16362004;
    public const int SDL_TEXTUREACCESS_STREAMING = 1;

    // ---- Event types ----
    public const uint SDL_QUIT = 0x100;
    public const uint SDL_KEYDOWN = 0x300;
    public const uint SDL_CONTROLLERBUTTONDOWN = 0x651;
    public const uint SDL_CONTROLLERDEVICEADDED = 0x653;
    public const uint SDL_CONTROLLERDEVICEREMOVED = 0x654;

    // ---- Keycodes (SDLK_*) ----
    public const int SDLK_ESCAPE = 27;
    public const int SDLK_TAB = 9;
    public const int SDLK_SPACE = 32;
    public const int SDLK_r = 'r';

    // ---- Scancodes (for held keys via SDL_GetKeyboardState) ----
    public const int SDL_SCANCODE_A = 4;
    public const int SDL_SCANCODE_D = 7;
    public const int SDL_SCANCODE_E = 8;
    public const int SDL_SCANCODE_Q = 20;
    public const int SDL_SCANCODE_S = 22;
    public const int SDL_SCANCODE_W = 26;
    public const int SDL_SCANCODE_RIGHT = 79;
    public const int SDL_SCANCODE_LEFT = 80;
    public const int SDL_SCANCODE_DOWN = 81;
    public const int SDL_SCANCODE_UP = 82;

    // ---- Game controller axes / buttons (SDL_GameControllerAxis / Button) ----
    public const int SDL_CONTROLLER_AXIS_LEFTX = 0;
    public const int SDL_CONTROLLER_AXIS_LEFTY = 1;
    public const int SDL_CONTROLLER_AXIS_RIGHTX = 2;
    public const int SDL_CONTROLLER_AXIS_RIGHTY = 3;
    public const int SDL_CONTROLLER_AXIS_TRIGGERLEFT = 4;
    public const int SDL_CONTROLLER_AXIS_TRIGGERRIGHT = 5;

    public const byte SDL_CONTROLLER_BUTTON_A = 0;
    public const byte SDL_CONTROLLER_BUTTON_B = 1;
    public const byte SDL_CONTROLLER_BUTTON_X = 2;
    public const byte SDL_CONTROLLER_BUTTON_Y = 3;
    public const byte SDL_CONTROLLER_BUTTON_BACK = 4;
    public const byte SDL_CONTROLLER_BUTTON_START = 6;
    public const byte SDL_CONTROLLER_BUTTON_LEFTSHOULDER = 9;
    public const byte SDL_CONTROLLER_BUTTON_RIGHTSHOULDER = 10;

    /// <summary>
    /// SDL_Event is a 56-byte union. Only the fields this app reads are mapped:
    ///   SDL_KeyboardEvent:         type@0 timestamp@4 windowID@8 state@12 repeat@13 keysym{scancode@16 sym@20 mod@24}
    ///   SDL_ControllerDeviceEvent: type@0 timestamp@4 which@8
    ///   SDL_ControllerButtonEvent: type@0 timestamp@4 which@8 button@12 state@13
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public struct SDL_Event
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(8)] public int which;           // controller events
        [FieldOffset(12)] public byte cbutton;       // controller button events
        [FieldOffset(13)] public byte keyRepeat;     // keyboard events
        [FieldOffset(20)] public int keySym;         // keyboard events: keysym.sym
    }

    [DllImport(Lib)] public static extern int SDL_Init(uint flags);
    [DllImport(Lib)] public static extern void SDL_Quit();
    [DllImport(Lib)] private static extern IntPtr SDL_GetError();
    public static string GetError() => Marshal.PtrToStringUTF8(SDL_GetError()) ?? "";

    [DllImport(Lib)]
    public static extern IntPtr SDL_CreateWindow([MarshalAs(UnmanagedType.LPUTF8Str)] string title,
                                                 int x, int y, int w, int h, uint flags);
    [DllImport(Lib)] public static extern void SDL_DestroyWindow(IntPtr window);
    [DllImport(Lib)]
    public static extern void SDL_SetWindowTitle(IntPtr window, [MarshalAs(UnmanagedType.LPUTF8Str)] string title);

    [DllImport(Lib)] public static extern IntPtr SDL_CreateRenderer(IntPtr window, int index, uint flags);
    [DllImport(Lib)] public static extern void SDL_DestroyRenderer(IntPtr renderer);
    [DllImport(Lib)] public static extern int SDL_RenderSetLogicalSize(IntPtr renderer, int w, int h);
    [DllImport(Lib)] public static extern int SDL_RenderClear(IntPtr renderer);
    [DllImport(Lib)] public static extern int SDL_RenderCopy(IntPtr renderer, IntPtr texture, IntPtr srcRect, IntPtr dstRect);
    [DllImport(Lib)] public static extern void SDL_RenderPresent(IntPtr renderer);

    [DllImport(Lib)] public static extern IntPtr SDL_CreateTexture(IntPtr renderer, uint format, int access, int w, int h);
    [DllImport(Lib)] public static extern void SDL_DestroyTexture(IntPtr texture);
    [DllImport(Lib)] public static extern int SDL_UpdateTexture(IntPtr texture, IntPtr rect, IntPtr pixels, int pitch);

    [DllImport(Lib)] public static extern int SDL_PollEvent(out SDL_Event e);
    [DllImport(Lib)] public static extern byte* SDL_GetKeyboardState(IntPtr numkeys);

    [DllImport(Lib)] public static extern int SDL_NumJoysticks();
    [DllImport(Lib)] public static extern int SDL_IsGameController(int joystickIndex);   // SDL_bool
    [DllImport(Lib)] public static extern IntPtr SDL_GameControllerOpen(int joystickIndex);
    [DllImport(Lib)] public static extern void SDL_GameControllerClose(IntPtr controller);
    [DllImport(Lib)] public static extern int SDL_GameControllerGetAttached(IntPtr controller); // SDL_bool
    [DllImport(Lib)] public static extern short SDL_GameControllerGetAxis(IntPtr controller, int axis);
    [DllImport(Lib)] private static extern IntPtr SDL_GameControllerName(IntPtr controller);
    public static string GameControllerName(IntPtr c) => Marshal.PtrToStringUTF8(SDL_GameControllerName(c)) ?? "?";

    [DllImport(Lib)] public static extern ulong SDL_GetPerformanceCounter();
    [DllImport(Lib)] public static extern ulong SDL_GetPerformanceFrequency();
}
