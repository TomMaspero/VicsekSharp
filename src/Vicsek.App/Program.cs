using System.Numerics;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using Vicsek.Core;

const int PanelWidth = 320;
const int HistoryLength = 600;

var parameters = new SimulationParameters();
var sim = new Simulation(parameters);

bool paused = false;
bool colorByHeading = true;
bool showGrid = false;
int stepsPerFrame = 1;
float lineLength = 8;

var history = new float[HistoryLength];
int historyOffset = 0;
int historyCount = 0;

void Reset()
{
    sim = new Simulation(parameters);
    Array.Clear(history);
    historyOffset = 0;
    historyCount = 0;
}

void RecordOrder()
{
    history[historyOffset] = (float)sim.OrderParameter();
    historyOffset = (historyOffset + 1) % HistoryLength;
    historyCount = Math.Min(historyCount + 1, HistoryLength);
}

Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint);
Raylib.InitWindow(1280, 900, "VicsekSharp");
Raylib.SetWindowMinSize(800, 500);
Raylib.SetTargetFPS(60);
rlImGui.Setup(darkTheme: true);

var background = new Color(20, 22, 26, 255);
var gridColor = new Color(255, 255, 255, 25);
var plainColor = new Color(110, 170, 255, 255);

while (!Raylib.WindowShouldClose())
{
    if (!ImGui.GetIO().WantCaptureKeyboard)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Space)) paused = !paused;
        if (Raylib.IsKeyPressed(KeyboardKey.R)) Reset();
        if (Raylib.IsKeyPressed(KeyboardKey.S) && paused) { sim.Step(); RecordOrder(); }
    }

    if (!paused)
    {
        for (int s = 0; s < stepsPerFrame; s++) sim.Step();
        RecordOrder();
    }

    int screenW = Raylib.GetScreenWidth();
    int screenH = Raylib.GetScreenHeight();
    float viewSize = Math.Min(screenW - PanelWidth, screenH);
    float scale = viewSize / (float)sim.BoxSize;
    var origin = new Vector2((screenW - PanelWidth - viewSize) / 2, (screenH - viewSize) / 2);

    // World y points up (headings are counter-clockwise), screen y points down.
    Vector2 ToScreen(double x, double y) =>
        new(origin.X + (float)x * scale, origin.Y + (float)(sim.BoxSize - y) * scale);

    Raylib.BeginDrawing();
    Raylib.ClearBackground(background);

    Raylib.DrawRectangleLinesEx(new Rectangle(origin, viewSize, viewSize), 1, gridColor);
    if (showGrid)
    {
        for (int i = 1; i < sim.CellsPerSide; i++)
        {
            float offset = (float)(i * sim.CellSize) * scale;
            Raylib.DrawLineV(origin with { X = origin.X + offset }, new Vector2(origin.X + offset, origin.Y + viewSize), gridColor);
            Raylib.DrawLineV(origin with { Y = origin.Y + offset }, new Vector2(origin.X + viewSize, origin.Y + offset), gridColor);
        }
    }

    foreach (var p in sim.Particles)
    {
        var pos = ToScreen(p.X, p.Y);
        var tip = pos + new Vector2(MathF.Cos((float)p.Theta), -MathF.Sin((float)p.Theta)) * lineLength;
        var color = colorByHeading ? Raylib.ColorFromHSV((float)(p.Theta * 180 / Math.PI), 0.75f, 1f) : plainColor;
        Raylib.DrawLineV(pos, tip, color);
        Raylib.DrawCircleV(pos, 1.5f, color);
    }

    rlImGui.Begin();
    DrawPanel(screenW, screenH);
    rlImGui.End();

    Raylib.EndDrawing();
}

rlImGui.Shutdown();
Raylib.CloseWindow();

void DrawPanel(int screenW, int screenH)
{
    ImGui.SetNextWindowPos(new Vector2(screenW - PanelWidth, 0));
    ImGui.SetNextWindowSize(new Vector2(PanelWidth, screenH));
    ImGui.Begin("Vicsek", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);

    ImGui.Text($"Step {sim.StepCount}   {Raylib.GetFPS()} FPS");
    float phi = historyCount > 0 ? history[(historyOffset + HistoryLength - 1) % HistoryLength] : (float)sim.OrderParameter();
    ImGui.Text($"Order parameter phi = {phi:F3}");
    ImGui.PlotLines("##phi", ref history[0], historyCount == HistoryLength ? HistoryLength : historyCount,
        historyCount == HistoryLength ? historyOffset : 0, null, 0f, 1f, new Vector2(PanelWidth - 20, 80));

    ImGui.SeparatorText("Run");
    if (ImGui.Button(paused ? "Play (Space)" : "Pause (Space)")) paused = !paused;
    ImGui.SameLine();
    ImGui.BeginDisabled(!paused);
    if (ImGui.Button("Step (S)")) { sim.Step(); RecordOrder(); }
    ImGui.EndDisabled();
    ImGui.SameLine();
    if (ImGui.Button("Reset (R)")) Reset();
    ImGui.SliderInt("Steps/frame", ref stepsPerFrame, 1, 20);

    // Noise, radius and speed act on the running simulation.
    ImGui.SeparatorText("Model");
    float noise = (float)sim.Parameters.Noise;
    if (ImGui.SliderFloat("Noise eta", ref noise, 0f, (float)(2 * Math.PI), "%.3f rad"))
        sim.Parameters.Noise = parameters.Noise = noise;
    float radius = (float)sim.Parameters.Radius;
    if (ImGui.SliderFloat("Radius", ref radius, 1f, (float)(sim.BoxSize / 2)))
        sim.Parameters.Radius = parameters.Radius = radius;
    float speed = (float)sim.Parameters.Speed;
    if (ImGui.SliderFloat("Speed", ref speed, 0f, 10f))
        sim.Parameters.Speed = parameters.Speed = speed;

    // Count and box size need a new simulation.
    ImGui.SeparatorText("System (applied on reset)");
    int count = parameters.Count;
    if (ImGui.SliderInt("Particles", ref count, 10, 20000))
        parameters.Count = count;
    float box = (float)parameters.BoxSize;
    if (ImGui.SliderFloat("Box size", ref box, 100f, 5000f, "%.0f"))
    {
        parameters.BoxSize = box;
        parameters.Radius = Math.Min(parameters.Radius, box / 2);
    }
    float density = (float)(parameters.Count / (parameters.BoxSize * parameters.BoxSize) * Math.PI * parameters.Radius * parameters.Radius);
    ImGui.TextDisabled($"Mean neighbours per particle: {density:F1}");
    if (ImGui.Button("Apply and reset")) Reset();

    ImGui.SeparatorText("View");
    ImGui.Checkbox("Color by heading", ref colorByHeading);
    ImGui.Checkbox("Show cell grid", ref showGrid);
    ImGui.SliderFloat("Line length", ref lineLength, 2f, 30f, "%.0f px");

    ImGui.End();
}
