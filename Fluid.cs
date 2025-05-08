using Raylib_cs;
using System.Numerics;
using System.Text;

void LoadImage(int w, int h, string path, Grid<float> red, Grid<float> green, Grid<float> blue) {
    Image image = Raylib.LoadImage(path);

    Raylib.ImageResize(ref image, w, h);
    for (int y = 0; y < h; ++y) {
        for (int x = 0; x < w; ++x) {
            int c = x + 1;
            int r = y + 1;

            var colour = Raylib.GetImageColor(image, x, y);
            red[c, r]   += colour.R / (float)byte.MaxValue;
            green[c, r] += colour.G / (float)byte.MaxValue;
            blue[c, r]  += colour.B / (float)byte.MaxValue;
        }
    }    
}

const float fixedUpdatesPerSecond = 60.0f;
const int iteration_count = 12;

const int width  = 100;
const int height = 100;

const int width_with_border  = width + 2;
const int height_with_border = height + 2;

const float density_diffusion_rate = 0.0f;
const float velocity_viscosity_rate = 2.5f;

static void SetBoundary(Grid<bool> wall, BoundaryMode mode, int w, int h, Grid<float> in_out) {
    for (int r = 1; r <= h; ++r) {
        for (int c = 1; c <= w; ++c) {
            if (wall[c, r]) in_out[c, r] = 0.0f;
        }
    }

    for (int r = 1; r <= h; ++r) {
        for (int c = 1; c <= w; ++c) {
            if (wall[c, r]) {
                if (!wall[c + 1, r]) {
                    in_out[c, r] = mode == BoundaryMode.VelocityX ? -in_out[c + 1, r] : in_out[c + 1, r];
                }
                if (!wall[c - 1, r]) {
                    in_out[c, r] = mode == BoundaryMode.VelocityX ? -in_out[c - 1, r] : in_out[c - 1, r];
                }
                if (!wall[c, r + 1]) {
                    in_out[c, r] = mode == BoundaryMode.VelocityY ? -in_out[c, r + 1] : in_out[c, r + 1];
                }
                if (!wall[c, r - 1]) {
                    in_out[c, r] = mode == BoundaryMode.VelocityY ? -in_out[c, r - 1] : in_out[c, r - 1];
                }
            }
        }
    }

    // Average corners
    for (int r = 1; r <= h; ++r) {
        for (int c = 1; c <= w; ++c) {
            if (!wall[c, r]) continue;

            if (!wall[c + 1, r] && !wall[c, r + 1]) {
                in_out[c, r] = 0.5f * (in_out[c + 1, r] + in_out[c, r + 1]);
            }

            if (!wall[c - 1, r] && !wall[c, r + 1]) {
                in_out[c, r] = 0.5f * (in_out[c - 1, r] + in_out[c, r + 1]);
            }

            if (!wall[c - 1, r] && !wall[c, r - 1]) {
                in_out[c, r] = 0.5f * (in_out[c - 1, r] + in_out[c, r - 1]);
            }
            
            if (!wall[c + 1, r] && !wall[c, r - 1]) {
                in_out[c, r] = 0.5f * (in_out[c + 1, r] + in_out[c, r - 1]);
            }
        }
    }
    

    for (int r = 1; r <= h; ++r) {
        in_out[    0, r] = mode == BoundaryMode.VelocityX ? -in_out[1, r] : in_out[1, r];
        in_out[w + 1, r] = mode == BoundaryMode.VelocityX ? -in_out[w, r] : in_out[w, r];
    }
    
    for (int c = 1; c <= w; ++c) {
        in_out[c,     0] = mode == BoundaryMode.VelocityY ? -in_out[c, 1] : in_out[c, 1];
        in_out[c, h + 1] = mode == BoundaryMode.VelocityY ? -in_out[c, h] : in_out[c, h];
    }

    // Average corners
    in_out[    0,     0] = 0.5f * (in_out[1,     0] + in_out[    0, 1]);
    in_out[w + 1,     0] = 0.5f * (in_out[w,     0] + in_out[w + 1, 1]);
    in_out[    0, h + 1] = 0.5f * (in_out[1, h + 1] + in_out[    0, h]);
    in_out[w + 1, h + 1] = 0.5f * (in_out[w, h + 1] + in_out[w + 1, h]);   
}


static void Diffuse(Grid<bool> wall, BoundaryMode mode, int w, int h, float dt, float diffusion_rate, Grid<float> input, Grid<float> output) {
    float a = dt * diffusion_rate; // a = dt * diff * w * h

    output.Fill(0);

    for (int iter = 0; iter < iteration_count; ++iter) {
        for (int r = 1; r <= h; ++r) {
            for (int c = 1; c <= w; ++c) {
                if (wall[c, r]) continue;
                output[c, r] = (
                    input[c, r] + a * (
                        output[c - 1, r] + output[c + 1, r] + 
                        output[c, r - 1] + output[c, r + 1]
                    )
                ) / (1 + 4 * a); 
            }
        }
        SetBoundary(wall, mode, w, h, output);
    }
}

static Vector2 FindWall(Grid<bool> wall, int w, int h, Vector2 start, Vector2 end) {
    Vector2 dir = end - start;
    float len = dir.Length();
    int steps = (int)(len) * 2 + 1;
    Vector2 step = dir / steps;

    Vector2 pos = start;
    for (int i = 0; i < steps; ++i) {
        Vector2 prev = pos;
        pos += step;
        if (pos.X <= 0.5f || pos.X >= w + 0.5f || pos.Y <= 0.5f || pos.Y >= h + 0.5f) break;

        if (wall[(int)pos.X, (int)pos.Y]) {
            pos = prev;
            break;
        }
    }
    return Vector2.Clamp(pos, new(0.5f, 0.5f), new(w + 0.5f, h + 0.5f));
}


static void Advect(Grid<bool> wall, BoundaryMode mode, int w, int h, float dt, Grid<float> velocityX, Grid<float> velocityY, Grid<float> input, Grid<float> output) {
    for (int r = 1; r <= h; ++r) {
        for (int c = 1; c <= w; ++c) {
            if (wall[c, r]) continue;
            var pos = FindWall(wall, w, h, new(c, r), new(c - dt * velocityX[c, r], r - dt * velocityY[c, r]));
            float x = pos.X;
            float y = pos.Y;

            int c0 = (int)x; int c1 = c0 + 1;
            int r0 = (int)y; int r1 = r0 + 1;

            float s1 = x - c0; float s0 = 1 - s1;
            float t1 = y - r0; float t0 = 1 - t1;

            output[c, r] = 
                s0 * (t0 * input[c0, r0] + t1 * input[c0, r1]) +
                s1 * (t0 * input[c1, r0] + t1 * input[c1, r1]);
        }
    }
    SetBoundary(wall, mode, w, h, output);
}
static void Project(Grid<bool> wall, int w, int h, Grid<float> velocityX, Grid<float> velocityY, Grid<float> tempDivergance, Grid<float> tempPressure) {
    tempDivergance.Fill(0);
    tempPressure.Fill(0);
    
    float a = 1.0f; // a = 1.0f / N
    for (int r = 1; r <= h; ++r) {
        for (int c = 1; c <= w; ++c) {
            if (wall[c, r]) continue;
            tempDivergance[c, r] = -0.5f * a * (
                +velocityX[c + 1, r] - velocityX[c - 1, r]
                +velocityY[c, r + 1] - velocityY[c, r - 1]
            );
        }
    }
    SetBoundary(wall, BoundaryMode.Normal, w, h, tempDivergance);


    for (int iter = 0; iter < iteration_count; ++iter) {
        for (int r = 1; r <= h; ++r) {
            for (int c = 1; c <= w; ++c) {
                if (wall[c, r]) continue;
                tempPressure[c, r] = 0.25f * (
                    tempDivergance[c, r] + 
                    tempPressure[c + 1, r] + tempPressure[c - 1, r] + 
                    tempPressure[c, r + 1] + tempPressure[c, r - 1]
                );
            }
        }
        SetBoundary(wall, BoundaryMode.Normal, w, h, tempPressure);
    }

    for (int r = 1; r <= h; ++r) {
        for (int c = 1; c <= w; ++c) {
            velocityX[c, r] -= 0.5f / a * (tempPressure[c + 1, r] - tempPressure[c - 1, r]);
            velocityY[c, r] -= 0.5f / a * (tempPressure[c, r + 1] - tempPressure[c, r - 1]);
        }
    }
    SetBoundary(wall, BoundaryMode.VelocityX, w, h, velocityX);
    SetBoundary(wall, BoundaryMode.VelocityX, w, h, velocityY);
}

Grid<bool> wall       = new(width_with_border, height_with_border, false);
Grid<float> densityR  = new(width_with_border, height_with_border, 0);
Grid<float> densityG  = new(width_with_border, height_with_border, 0);
Grid<float> densityB  = new(width_with_border, height_with_border, 0);


Grid<float> velocityX = new(width_with_border, height_with_border, 0);
Grid<float> velocityY = new(width_with_border, height_with_border, 0);
Grid<float> tempX     = new(width_with_border, height_with_border, 0);
Grid<float> tempY     = new(width_with_border, height_with_border, 0);



void Simulate(float dt) {
    void DiffuseStep(ref Grid<float> density, ref Grid<float> tempX) {
        Diffuse(wall, BoundaryMode.Normal, width, height, dt, density_diffusion_rate, density, tempX);
        (density, tempX) = (tempX, density);
        Advect(wall, BoundaryMode.Normal, width, height, dt, velocityX, velocityY, density, tempX);
        (density, tempX) = (tempX, density);
    }

    DiffuseStep(ref densityR, ref tempX);
    DiffuseStep(ref densityG, ref tempX);
    DiffuseStep(ref densityB, ref tempX);
    
    Diffuse(wall, BoundaryMode.VelocityX, width, height, dt, velocity_viscosity_rate, velocityX, tempX);
    Diffuse(wall, BoundaryMode.VelocityY, width, height, dt, velocity_viscosity_rate, velocityY, tempY);
    (velocityX, tempX) = (tempX, velocityX);
    (velocityY, tempY) = (tempY, velocityY);
    Project(wall, width, height, velocityX, velocityY, tempX, tempY);

    Advect(wall, BoundaryMode.VelocityX, width, height, dt, velocityX, velocityY, velocityX, tempX);
    Advect(wall, BoundaryMode.VelocityY, width, height, dt, velocityX, velocityY, velocityY, tempY);
    (velocityX, tempX) = (tempX, velocityX);
    (velocityY, tempY) = (tempY, velocityY);
    Project(wall, width, height, velocityX, velocityY, tempX, tempY);
}

Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
Raylib.InitWindow(800, 800, "WFI");


float timeSinceFixedUpdate = 0.0f;

float densityAddedPerSecond  = 100.0f;
float velocityAddedPerSecond = 10000.0f;

bool showHelp = true;
bool showGrid = false;
bool showVelocityField = true;

string helpText = 
"""
F1 - show help
F2 - show grid
F3 - show velocity field
R - reset
LMB - add density
RMB - add wall
W,S,A,D - add velocity
Q, E - change added density
LCTRL, LSHIFT - change added velocity

""".ReplaceLineEndings("\n");

while (!Raylib.WindowShouldClose()) {
    int w = Raylib.GetScreenWidth();
    int h = Raylib.GetScreenHeight();

    Vector2 cellSize = new(float.Min((float)w / width_with_border, (float)h / height_with_border));
    Vector2 gridOffset = Vector2.Zero;

    // User input
    {
        if (Raylib.IsKeyPressed(KeyboardKey.F1)) {
            showHelp = !showHelp;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.F2)) {
            showGrid = !showGrid;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.F3)) {
            showVelocityField = !showVelocityField;
        } 
        
        if (Raylib.IsKeyPressed(KeyboardKey.E)) {
            densityAddedPerSecond *= 10.0f;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Q)) {
            densityAddedPerSecond /= 10.0f;
        }
        densityAddedPerSecond = float.Clamp(densityAddedPerSecond, 1.0f, 1e8f);
        
        if (Raylib.IsKeyPressed(KeyboardKey.LeftShift)) {
            velocityAddedPerSecond *= 10.0f;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.LeftControl)) {
            velocityAddedPerSecond /= 10.0f;
        }
        velocityAddedPerSecond = float.Clamp(velocityAddedPerSecond, 1.0f, 1e8f);

        if (Raylib.IsKeyPressed(KeyboardKey.R)) {
            for (int y = 0; y < height_with_border; ++y) {
                for (int x = 0; x < width_with_border; ++x) {
                    velocityX[x, y] = velocityY[x, y] = densityR[x, y] = densityG[x, y] = densityB[x, y] = 0.0f;
                }
            }
        }
        
        Vector2 cell = (Raylib.GetMousePosition() - gridOffset) / cellSize;
        int c = (int)cell.X;
        int r = (int)cell.Y;

        if (Raylib.IsMouseButtonDown(MouseButton.Right)) {
            for (int x = 0; x < 3; ++x) {
                for (int y = 0; y < 3; ++y) {
                    int c0 = c + x;
                    int r0 = r + y;

                    if (0 <= c0 && c0 < width_with_border && 0 <= r0 && r0 < height_with_border) {
                        wall[c0, r0] = true; 
                    } 
                }
            }
        }

        if (1 <= c && c <= width && 1 <= r && r <= height) {  
            if (Raylib.IsMouseButtonDown(MouseButton.Left)) {
                densityR[c, r] += Raylib.GetFrameTime() * densityAddedPerSecond; 
            }
               
            if (Raylib.IsKeyDown(KeyboardKey.W)) {
                velocityY[c, r] -= Raylib.GetFrameTime() * velocityAddedPerSecond;
            }
            if (Raylib.IsKeyDown(KeyboardKey.S)) {
                velocityY[c, r] += Raylib.GetFrameTime() * velocityAddedPerSecond;
            } 
            
            if (Raylib.IsKeyDown(KeyboardKey.A)) {
                velocityX[c, r] -= Raylib.GetFrameTime() * velocityAddedPerSecond;
            } 
            if (Raylib.IsKeyDown(KeyboardKey.D)) {
                velocityX[c, r] += Raylib.GetFrameTime() * velocityAddedPerSecond;
            } 
        }

        if (Raylib.IsFileDropped()) {
            string[] files = Raylib.GetDroppedFiles();
            LoadImage(width, height, files[0], densityR, densityG, densityB);
        }
    }
    
    if (Raylib.IsWindowFocused()) {
        timeSinceFixedUpdate += Raylib.GetFrameTime();
    }  
 
    const float fixedUpdateDelta = 1.0f / fixedUpdatesPerSecond;
    while (timeSinceFixedUpdate >= fixedUpdateDelta) {
        Simulate(fixedUpdateDelta);
        timeSinceFixedUpdate -= fixedUpdateDelta;
    }    


    Raylib.BeginDrawing();
    Raylib.IsWindowResized();

    Raylib.ClearBackground(Color.White);


    for (int r = 0; r < height_with_border; ++r) {
        for (int c = 0; c < width_with_border; ++c) {
            Color colour = new(
                (int)float.Clamp(densityR[c, r] * byte.MaxValue, 0.0f, byte.MaxValue),
                (int)float.Clamp(densityG[c, r] * byte.MaxValue, 0.0f, byte.MaxValue),
                (int)float.Clamp(densityB[c, r] * byte.MaxValue, 0.0f, byte.MaxValue)
            );

            Vector2 pos = gridOffset + cellSize * new Vector2(c, r);
            Rectangle cell = new(pos, cellSize);
            Raylib.DrawRectangleRec(cell, colour);
            if (showGrid) {
                Raylib.DrawRectangleLinesEx(cell, 1.0f, new(50, 50, 50));
            }

            if (wall[c, r]) {
                Raylib.DrawRectangleLinesEx(cell, 1.0f, Color.Green);
            }
        }
    }

    if (showVelocityField) {
        for (int r = 0; r < height_with_border; ++r) {
            for (int c = 0; c < width_with_border; ++c) {
                Vector2 center = gridOffset + cellSize * new Vector2(c + 0.5f, r + 0.5f);
                Vector2 vel = new(velocityX[c, r], velocityY[c, r]);
                float len = vel.Length();
                if (len > 1e-10) {
                    Vector2 dir = vel * (cellSize.X * 0.5f / len);
                    Raylib.DrawLineV(center, center + vel * cellSize * fixedUpdateDelta, Color.Magenta);
                    // Raylib.DrawLineV(center, center + dir, Color.Blue);
                } 
            }
        }
    }

    StringBuilder sb = new();
    if (showHelp) sb.Append(helpText);
    sb.Append($"density to add: {densityAddedPerSecond}\n");
    sb.Append($"velocity to add: {velocityAddedPerSecond}\n");
        
    Raylib.DrawTextEx(Raylib.GetFontDefault(), sb.ToString(), gridOffset, 28.0f, 1.0f, Color.White);
    Raylib.EndDrawing();
}
Raylib.CloseWindow();
readonly struct Grid<T> { 
    readonly int width;
    readonly T[] array;    
    public Grid(int width, int height, T initialValue) {
        this.width = width;
        array = new T[width * height];
        Fill(initialValue);
    }
    public void Fill(T value) {
        Array.Fill(array, value);
    }
    public readonly ref T this[int c, int r] => ref array[width * r + c];
};

enum BoundaryMode {Normal, VelocityX, VelocityY };

