using System.Numerics;
using SoftEngine.Core;
using static SoftEngine.App.SDL;

namespace SoftEngine.App;

internal static unsafe class Program
{
    // Internal render resolution; SDL scales it to the window (letterboxed).
    private const int Width = 960;
    private const int Height = 720;

    private const float FieldOfView = 0.78f;      // radians (~45°), same as the 2013 engine
    private const float Near = 0.1f;
    private const float Far = 1000f;

    private const float TurnRate = 1.8f;          // rad/sec at full stick
    private const float StickDeadzone = 0.12f;    // F310 sticks don't always re-center exactly
    private const bool InvertPitch = false;       // flip if stick-up feels backwards

    private const uint Black = 0xFF000000;
    private const uint Green = 0xFF30FF60;        // vector-monitor green

    private static int Main(string[] args)
    {
        var meshes = LoadMeshes(args);
        if (meshes.Count == 0)
        {
            Console.Error.WriteLine("Usage: SoftEngine.App <file.obj | folder> [more...]");
            return 1;
        }

        Setup();
        if (SDL_Init(SDL_INIT_VIDEO | SDL_INIT_GAMECONTROLLER) != 0)
        {
            Console.Error.WriteLine($"SDL_Init failed: {GetError()}");
            return 1;
        }

        IntPtr window = SDL_CreateWindow("Soft Engine", SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED,
                                         Width, Height, SDL_WINDOW_SHOWN | SDL_WINDOW_RESIZABLE);
        IntPtr renderer = SDL_CreateRenderer(window, -1, SDL_RENDERER_ACCELERATED | SDL_RENDERER_PRESENTVSYNC);
        if (renderer == IntPtr.Zero)
            renderer = SDL_CreateRenderer(window, -1, 0);   // fall back to whatever SDL can give us
        SDL_RenderSetLogicalSize(renderer, Width, Height);
        IntPtr texture = SDL_CreateTexture(renderer, SDL_PIXELFORMAT_ARGB8888, SDL_TEXTUREACCESS_STREAMING, Width, Height);

        if (window == IntPtr.Zero || renderer == IntPtr.Zero || texture == IntPtr.Zero)
        {
            Console.Error.WriteLine($"SDL setup failed: {GetError()}");
            SDL_Quit();
            return 1;
        }

        IntPtr pad = OpenFirstController();

        var fb = new FrameBuffer(Width, Height);
        var wireframe = new WireframeRenderer();
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, (float)Width / Height, Near, Far);

        int current = 0;
        var model = new Frame();                       // model orientation: Margolin unit vectors
        float distance = 0f;
        bool autoSpin = false;
        SelectMesh(0);

        ulong freq = SDL_GetPerformanceFrequency();
        ulong last = SDL_GetPerformanceCounter();
        double titleTimer = 0;
        int frames = 0;
        bool running = true;

        void SelectMesh(int index)
        {
            current = (index % meshes.Count + meshes.Count) % meshes.Count;
            model.Reset();
            model.Rotate(Vector3.UnitY, 0.6f);         // start from a three-quarter view
            model.Rotate(Vector3.UnitX, 0.35f);
            distance = FitDistance(meshes[current]);
        }

        while (running)
        {
            // ---------- events ----------
            while (SDL_PollEvent(out var e) != 0)
            {
                switch (e.type)
                {
                    case SDL_QUIT:
                        running = false;
                        break;

                    case SDL_KEYDOWN when e.keyRepeat == 0:
                        switch (e.keySym)
                        {
                            case SDLK_ESCAPE: running = false; break;
                            case SDLK_TAB: SelectMesh(current + 1); break;
                            case SDLK_r: SelectMesh(current); break;
                            case SDLK_SPACE: autoSpin = !autoSpin; break;
                        }
                        break;

                    case SDL_CONTROLLERDEVICEADDED:
                        if (pad == IntPtr.Zero)
                        {
                            pad = SDL_GameControllerOpen(e.which);   // 'which' = device index here
                            if (pad != IntPtr.Zero) Console.WriteLine($"Controller: {GameControllerName(pad)}");
                        }
                        break;

                    case SDL_CONTROLLERDEVICEREMOVED:
                        if (pad != IntPtr.Zero && SDL_GameControllerGetAttached(pad) == 0)
                        {
                            SDL_GameControllerClose(pad);
                            pad = OpenFirstController();
                        }
                        break;

                    case SDL_CONTROLLERBUTTONDOWN:
                        switch (e.cbutton)
                        {
                            case SDL_CONTROLLER_BUTTON_RIGHTSHOULDER: SelectMesh(current + 1); break;
                            case SDL_CONTROLLER_BUTTON_LEFTSHOULDER: SelectMesh(current - 1); break;
                            case SDL_CONTROLLER_BUTTON_A: SelectMesh(current); break;
                            case SDL_CONTROLLER_BUTTON_X: autoSpin = !autoSpin; break;
                            case SDL_CONTROLLER_BUTTON_BACK: running = false; break;
                        }
                        break;
                }
            }

            ulong now = SDL_GetPerformanceCounter();
            float dt = MathF.Min((float)((now - last) / (double)freq), 0.1f);
            last = now;

            // ---------- input -> rotation ----------
            float yaw = 0, pitch = 0, roll = 0, zoom = 0;

            if (pad != IntPtr.Zero)
            {
                yaw += Axis(pad, SDL_CONTROLLER_AXIS_LEFTX);
                pitch += Axis(pad, SDL_CONTROLLER_AXIS_LEFTY) * (InvertPitch ? -1f : 1f);
                roll += Axis(pad, SDL_CONTROLLER_AXIS_RIGHTX);
                zoom += Axis(pad, SDL_CONTROLLER_AXIS_RIGHTY);
            }

            byte* keys = SDL_GetKeyboardState(IntPtr.Zero);
            if (keys[SDL_SCANCODE_LEFT] != 0) yaw -= 1;
            if (keys[SDL_SCANCODE_RIGHT] != 0) yaw += 1;
            if (keys[SDL_SCANCODE_UP] != 0) pitch -= 1;
            if (keys[SDL_SCANCODE_DOWN] != 0) pitch += 1;
            if (keys[SDL_SCANCODE_Q] != 0) roll -= 1;
            if (keys[SDL_SCANCODE_E] != 0) roll += 1;
            if (keys[SDL_SCANCODE_W] != 0) zoom -= 1;
            if (keys[SDL_SCANCODE_S] != 0) zoom += 1;

            if (autoSpin) yaw += 0.35f;

            // Rotate the model's unit vectors about the CAMERA's axes (camera is fixed,
            // looking down -Z), so the stick always turns the model the way you push it.
            model.Rotate(Vector3.UnitY, yaw * TurnRate * dt);
            model.Rotate(Vector3.UnitX, pitch * TurnRate * dt);
            model.Rotate(Vector3.UnitZ, -roll * TurnRate * dt);
            model.Orthonormalize();

            var mesh = meshes[current];
            // Zoom is linear so you can fly the camera INTO the model and watch near-plane clipping.
            distance += zoom * mesh.Radius * 1.5f * dt;
            distance = Math.Clamp(distance, -3f * mesh.Radius, 30f * mesh.Radius);

            // ---------- render ----------
            var world = Matrix4x4.CreateTranslation(-mesh.Center) * model.ToMatrix(Vector3.Zero);
            var view = Matrix4x4.CreateLookAt(new Vector3(0, 0, distance), new Vector3(0, 0, distance - 1f), Vector3.UnitY);

            fb.Clear(Black);
            wireframe.Draw(fb, mesh, world, view, projection, Near, Green);

            fixed (uint* pixels = fb.Pixels)
                SDL_UpdateTexture(texture, IntPtr.Zero, (IntPtr)pixels, Width * sizeof(uint));
            SDL_RenderClear(renderer);
            SDL_RenderCopy(renderer, texture, IntPtr.Zero, IntPtr.Zero);
            SDL_RenderPresent(renderer);

            // ---------- title bar stats ----------
            frames++;
            titleTimer += dt;
            if (titleTimer >= 0.5)
            {
                SDL_SetWindowTitle(window,
                    $"{mesh.Name}  ({current + 1}/{meshes.Count})  {mesh.Vertices.Length} verts, " +
                    $"{mesh.Edges.Length} edges  {frames / titleTimer:0} fps");
                frames = 0;
                titleTimer = 0;
            }
        }

        if (pad != IntPtr.Zero) SDL_GameControllerClose(pad);
        SDL_DestroyTexture(texture);
        SDL_DestroyRenderer(renderer);
        SDL_DestroyWindow(window);
        SDL_Quit();
        return 0;
    }

    /// <summary>Camera distance that fits the whole mesh in view, with a little margin.</summary>
    private static float FitDistance(Mesh mesh) => mesh.Radius / MathF.Tan(FieldOfView * 0.5f);

    /// <summary>Axis value in -1..1 with a rescaled deadzone (smooth start at the deadzone edge).</summary>
    private static float Axis(IntPtr pad, int axis)
    {
        float v = SDL_GameControllerGetAxis(pad, axis) / 32767f;
        float a = MathF.Abs(v);
        if (a < StickDeadzone) return 0f;
        return MathF.Sign(v) * MathF.Min((a - StickDeadzone) / (1f - StickDeadzone), 1f);
    }

    private static IntPtr OpenFirstController()
    {
        for (int i = 0; i < SDL_NumJoysticks(); i++)
        {
            if (SDL_IsGameController(i) == 0) continue;
            IntPtr c = SDL_GameControllerOpen(i);
            if (c != IntPtr.Zero)
            {
                Console.WriteLine($"Controller: {GameControllerName(c)}");
                return c;
            }
        }
        return IntPtr.Zero;
    }

    private static List<Mesh> LoadMeshes(string[] args)
    {
        var files = new List<string>();
        foreach (var arg in args)
        {
            if (Directory.Exists(arg))
                files.AddRange(Directory.GetFiles(arg, "*.obj").OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
            else
                files.Add(arg);
        }

        var meshes = new List<Mesh>();
        foreach (var file in files)
        {
            try
            {
                var m = ObjLoader.Load(file);
                Console.WriteLine($"Loaded {file}: {m.Vertices.Length} vertices, {m.Edges.Length} edges");
                meshes.Add(m);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Skipping {file}: {ex.Message}");
            }
        }
        return meshes;
    }
}
