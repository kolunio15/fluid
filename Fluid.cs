using Raylib_cs;
using System.Numerics;

int width = 32;
int height = 32;

int width_with_border  = width + 2;
int height_with_border = height + 2;

static void Simulate() {
    DensityStep();
    VelocityStep();
}

static void DensityStep() {

}
static void VelocityStep() {

}

static void SetBoundary(int w, int h, BoundaryMode mode, Grid<float> in_out) {
    for (int r = 1; r <= h; ++r) {
        in_out[    0, r] = mode == BoundaryMode.VelocityX ? -in_out[1, r] : in_out[1, r];
        in_out[w + 1, r] = mode == BoundaryMode.VelocityX ? -in_out[w, r] : in_out[w, r];
    }
    
    for (int c = 1; c <= w; ++c) {
        in_out[c,     0] = mode == BoundaryMode.VelocityY ? -in_out[c, 1] : in_out[c, 1];
        in_out[c, h + 1] = mode == BoundaryMode.VelocityY ? -in_out[c, h] : in_out[c, h];
    }

    // Corners are averaged
    in_out[    0,     0] = 0.5f * (in_out[1,     0] + in_out[    0, 1]);
    in_out[w + 1,     0] = 0.5f * (in_out[w,     0] + in_out[w + 1, 1]);
    in_out[    0, h + 1] = 0.5f * (in_out[1, h + 1] + in_out[    0, h]);
    in_out[w + 1, h + 1] = 0.5f * (in_out[w, h + 1] + in_out[w + 1, h]);   
}

Grid<float> density   = new(width_with_border, height_with_border, 0);
Grid<float> velocityX = new(width_with_border, height_with_border, 0);
Grid<float> velocityY = new(width_with_border, height_with_border, 0);
Grid<float> tempX     = new(width_with_border, height_with_border, 0);
Grid<float> tempY     = new(width_with_border, height_with_border, 0);

Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
Raylib.InitWindow(800, 800, "WFI");
while (!Raylib.WindowShouldClose()) {
    int w = Raylib.GetScreenWidth();
    int h = Raylib.GetScreenHeight();

    // TODO: Keep aspect ratio
    Vector2 gridOffset = Vector2.Zero;
    Vector2 cellSize = new Vector2(int.Min(w, h)) / new Vector2(width_with_border, height_with_border);

    Simulate();

    Raylib.BeginDrawing();
    Raylib.IsWindowResized();

    Raylib.ClearBackground(Color.White);

    for (int r = 0; r < height_with_border; ++r) {
        for (int c = 0; c < width_with_border; ++c) {
            byte intensity = (byte)float.Clamp(density[c, r] * byte.MaxValue, 0, byte.MaxValue);
            Color colour = new(intensity, intensity, intensity);

            Vector2 pos = cellSize * new Vector2(c, r);

            Rectangle cell = new(pos, cellSize);
            Raylib.DrawRectangleRec(cell, colour);
            Raylib.DrawRectangleLinesEx(cell, 1.0f, new(50, 50, 50));
        }
    }

    Raylib.EndDrawing();
}
Raylib.CloseWindow();

readonly struct Grid<T> { 
    readonly int width;
    readonly T[] array;    
    public Grid(int width, int height, T initialValue) {
        this.width = width;
        array = new T[width * height];
        Array.Fill(array, initialValue);
    }
    public readonly ref T this[int c, int r] => ref array[width * r + c];
};

enum BoundaryMode {Normal, VelocityX, VelocityY };

