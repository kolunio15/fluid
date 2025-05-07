using Raylib_cs;
using System.Numerics;

const int width = 32;
const int height = 32;

const int width_with_border  = width + 2;
const int height_with_border = height + 2;

const float density_diffusion_rate = 10.0f;
const float velocity_viscosity_rate = 5.0f;


static void SetBoundary(BoundaryMode mode, int w, int h, Grid<float> in_out) {
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

const int iteration_count = 20;
static void Diffuse(BoundaryMode mode, int w, int h, float dt, float diffusion_rate, Grid<float> input, Grid<float> output) {
    float a = dt * diffusion_rate; // a = dt * diff * w * h

    for (int iter = 0; iter < iteration_count; ++iter) {
        for (int r = 1; r <= h; ++r) {
            for (int c = 1; c <= w; ++c) {
                output[c, r] = (
                    input[c, r] + a * (
                        output[c - 1, r] + output[c + 1, r] + 
                        output[c, r - 1] + output[c, r + 1]
                    )
                ) / (1 + 4 * a); 
            }
        }
        SetBoundary(mode, w, h, output);
    }
}
static void Advect(BoundaryMode mode, int w, int h, float dt, Grid<float> velocityX, Grid<float> velocityY, Grid<float> input, Grid<float> output) {
    for (int r = 1; r <= h; ++r) {
        for (int c = 1; c <= w; ++c) {
            float x = float.Clamp(c - dt * velocityX[c, r], 0.5f, w + 0.5f);
            float y = float.Clamp(r - dt * velocityY[c, r], 0.5f, h + 0.5f);

            int c0 = (int)x; int c1 = c0 + 1;
            int r0 = (int)y; int r1 = r0 + 1;

            float s1 = x - c0; float s0 = 1 - s1;
            float t1 = y - r0; float t0 = 1 - t1;

            output[c, r] = 
                s0 * (t0 * input[c0, r0] + t1 * input[c0, r1]) +
                s1 * (t0 * input[c1, r0] + t1 * input[c1, r1]);
        }
    }
    SetBoundary(mode, w, h, output);
}
static void Project(int w, int h, Grid<float> velocityX, Grid<float> velocityY, Grid<float> tempDivergance, Grid<float> tempPressure) {
    float a = 1.0f; // a = 1.0f / N
    for (int r = 1; r <= h; ++r) {
        for (int c = 1; c <= w; ++c) {
            tempDivergance[c, r] = -0.5f * a * (
                +velocityX[c + 1, r] - velocityX[c - 1, r]
                +velocityY[c, r + 1] - velocityY[c, r - 1]
            );
            tempPressure[c, r] = 0.0f;
        }
    }
    SetBoundary(BoundaryMode.Normal, w, h, tempDivergance);
    SetBoundary(BoundaryMode.Normal, w, h, tempPressure);

    for (int iter = 0; iter < iteration_count; ++iter) {
        for (int r = 1; r <= h; ++r) {
            for (int c = 1; c <= w; ++c) {
                tempPressure[c, r] = 0.25f * (
                    tempDivergance[c, r] + 
                    tempPressure[c + 1, r] + tempPressure[c - 1, r] + 
                    tempPressure[c, r + 1] + tempPressure[c, r - 1]
                );
            }
        }
    }

    for (int r = 1; r <= h; ++r) {
        for (int c = 1; c <= w; ++c) {
            velocityX[c, r] -= 0.5f / a * (tempPressure[c + 1, r] - tempPressure[c - 1, r]);
            velocityY[c, r] -= 0.5f / a * (tempPressure[c, r + 1] - tempPressure[c, r - 1]);
        }
    }
    SetBoundary(BoundaryMode.VelocityX, w, h, velocityX);
    SetBoundary(BoundaryMode.VelocityX, w, h, velocityY);
}

Grid<float> density   = new(width_with_border, height_with_border, 0);
Grid<float> velocityX = new(width_with_border, height_with_border, 0);
Grid<float> velocityY = new(width_with_border, height_with_border, 0);
Grid<float> tempX     = new(width_with_border, height_with_border, 0);
Grid<float> tempY     = new(width_with_border, height_with_border, 0);


void Simulate(float dt) {
    Diffuse(BoundaryMode.Normal, width, height, dt, density_diffusion_rate, density, tempX);
    (density, tempX) = (tempX, density);
    Advect(BoundaryMode.Normal, width, height, dt, velocityX, velocityY, density, tempX);
    (density, tempX) = (tempX, density);

    Diffuse(BoundaryMode.VelocityX, width, height, dt, velocity_viscosity_rate, velocityX, tempX);
    Diffuse(BoundaryMode.VelocityY, width, height, dt, velocity_viscosity_rate, velocityY, tempY);
    (velocityX, tempX) = (tempX, velocityX);
    (velocityY, tempY) = (tempY, velocityY);
    Project(width, height, velocityX, velocityY, tempX, tempY);

    Advect(BoundaryMode.VelocityX, width, height, dt, velocityX, velocityY, velocityX, tempX);
    Advect(BoundaryMode.VelocityY, width, height, dt, velocityX, velocityY, velocityY, tempY);
    (velocityX, tempX) = (tempX, velocityX);
    (velocityY, tempY) = (tempY, velocityY);
    Project(width, height, velocityX, velocityY, tempX, tempY);
}

Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
Raylib.InitWindow(800, 800, "WFI");


float timeSinceFixedUpdate = 0.0f;

while (!Raylib.WindowShouldClose()) {
    int w = Raylib.GetScreenWidth();
    int h = Raylib.GetScreenHeight();

    // TODO: Keep aspect ratio
    Vector2 gridOffset = Vector2.Zero;
    Vector2 cellSize = new Vector2(int.Min(w, h)) / new Vector2(width_with_border, height_with_border);


    // User input
    {
        Vector2 cell = (Raylib.GetMousePosition() - gridOffset) / cellSize;
        int c = (int)cell.X;
        int r = (int)cell.Y;

        if (1 <= c && c <= width && 1 <= r && r <= height) {  
            if (Raylib.IsMouseButtonDown(MouseButton.Left)) {
                density[c, r] += Raylib.GetFrameTime() * 100.0f; 
            }

            if (Raylib.IsKeyDown(KeyboardKey.W)) {
                velocityY[c, r] -= Raylib.GetFrameTime() * 100.0f;
            }
            if (Raylib.IsKeyDown(KeyboardKey.S)) {
                velocityY[c, r] += Raylib.GetFrameTime() * 100.0f;
            } 
            
            if (Raylib.IsKeyDown(KeyboardKey.A)) {
                velocityX[c, r] -= Raylib.GetFrameTime() * 100.0f;
            } 
            if (Raylib.IsKeyDown(KeyboardKey.D)) {
                velocityX[c, r] += Raylib.GetFrameTime() * 100.0f;
            } 
        }
    }
    

    timeSinceFixedUpdate += Raylib.GetFrameTime();
    
    const float fixedUpdateDelta = 1.0f / 120.0f;
    while (timeSinceFixedUpdate >= fixedUpdateDelta) {
        Simulate(fixedUpdateDelta);
        timeSinceFixedUpdate -= fixedUpdateDelta;
    }    


    Raylib.BeginDrawing();
    Raylib.IsWindowResized();

    Raylib.ClearBackground(Color.White);


    for (int r = 0; r < height_with_border; ++r) {
        for (int c = 0; c < width_with_border; ++c) {
            int intensity = (int)float.Clamp(density[c, r] * byte.MaxValue, 0, byte.MaxValue);
            Color colour = new(intensity, 0, 0);

            Vector2 pos = cellSize * new Vector2(c, r);

            Rectangle cell = new(pos, cellSize);
            Raylib.DrawRectangleRec(cell, colour);
            Raylib.DrawRectangleLinesEx(cell, 1.0f, new(50, 50, 50));
        }
    }

    for (int r = 0; r < height_with_border; ++r) {
        for (int c = 0; c < width_with_border; ++c) {
            Vector2 center = cellSize * new Vector2(c + 0.5f, r + 0.5f);
            Vector2 vel = new(velocityX[c, r], velocityY[c, r]);
            float len = vel.Length();
            if (len > 1e-10) {
                Vector2 dir = vel * (cellSize.X * 0.5f / len);
                Raylib.DrawLineV(center, center + dir * len, Color.Magenta);
                Raylib.DrawLineV(center, center + dir, Color.Blue);
            } 
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

