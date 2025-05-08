using Raylib_cs;
using System.Numerics;
using System.Text;

static void LoadImage(int w, int h, string path, Grid<float> red, Grid<float> green, Grid<float> blue) {
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
const int height = 120;

const int width_with_border  = width + 2;
const int height_with_border = height + 2;

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



void Simulate(float dt, float diffusionRate, float viscosityRate) {
    void DiffuseStep(ref Grid<float> density, ref Grid<float> tempX) {
        Diffuse(wall, BoundaryMode.Normal, width, height, dt, diffusionRate, density, tempX);
        (density, tempX) = (tempX, density);
        Advect(wall, BoundaryMode.Normal, width, height, dt, velocityX, velocityY, density, tempX);
        (density, tempX) = (tempX, density);
    }

    DiffuseStep(ref densityR, ref tempX);
    DiffuseStep(ref densityG, ref tempX);
    DiffuseStep(ref densityB, ref tempX);
    
    Diffuse(wall, BoundaryMode.VelocityX, width, height, dt, viscosityRate, velocityX, tempX);
    Diffuse(wall, BoundaryMode.VelocityY, width, height, dt, viscosityRate, velocityY, tempY);
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
Raylib.InitWindow(1200, 800, "WFI");

float timeSinceFixedUpdate = 0.0f;

bool showHelp = true;
bool showGrid = false;
bool showVelocityField = true;

string helpText = 
"""
F1 - show help
F2 - show grid
F3 - show velocity field
R - reset fluid
2 - reset walls
LMB - add density
RMB - add wall
W,S,A,D - add velocity
Q, E - change added density
LCTRL, LSHIFT - change added velocity

""".ReplaceLineEndings("\n");

Slider diffusionRate = new("diffusion",       0.0f, 10.0f,     1.0f);
Slider viscosityRate = new("viscosity",       0.0f, 10.0f,     1.0f);
Slider densityToAdd  = new("density to add",  1.0f,   1e5f,   100.0f);
Slider velocityToAdd = new("velocity to add", 1.0f,   1e8f, 10000.0f);
Slider addColourR    = new("R",               0.0f,   1.0f,     1.0f);
Slider addColourG    = new("G",               0.0f,   1.0f,     1.0f);
Slider addColourB    = new("B",               0.0f,   1.0f,     1.0f);

List<Slider> sliders = [diffusionRate, viscosityRate, densityToAdd, velocityToAdd, addColourR, addColourG, addColourB];

while (!Raylib.WindowShouldClose()) {
    int w = Raylib.GetScreenWidth();
    int h = Raylib.GetScreenHeight();

    
    Vector2 cellSize = new(float.Min((float)w / width_with_border, (float)h / height_with_border));
    Vector2 gridOffset = Vector2.Zero;
    Vector2 gridSize = cellSize * new Vector2(width_with_border, height_with_border);
    Vector2 windowSize = new(w, h);

    // Gui layout
    {
        float padding = 10.0f;
        float y = 0.0f;
        float guiWidth = windowSize.X - gridSize.X - gridOffset.X -  2 * padding;
        float sliderHeight = 40.0f;

        foreach (Slider s in sliders) {
            s.SetLayout(new(gridOffset.X + gridSize.X + padding, y), new(guiWidth, sliderHeight));
            y += sliderHeight + padding;
        }
    }

    // User input
    {
        foreach (Slider s in sliders) s.HandleMouse();

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
            densityToAdd.Value *= 2.0f;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Q)) {
            densityToAdd.Value /= 2.0f;
        }
        
        if (Raylib.IsKeyPressed(KeyboardKey.LeftShift)) {
            velocityToAdd.Value *= 2.0f;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.LeftControl)) {
            velocityToAdd.Value /= 2.0f;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.R)) {
            densityR.Fill(0.0f);
            densityG.Fill(0.0f);
            densityB.Fill(0.0f);

            velocityX.Fill(0.0f);
            velocityY.Fill(0.0f);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Two)) {
            wall.Fill(false);
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
                float amount = Raylib.GetFrameTime() * densityToAdd.Value;
                densityR[c, r] += addColourR.Value * amount; 
                densityG[c, r] += addColourG.Value * amount; 
                densityB[c, r] += addColourB.Value * amount; 
            }
               
            if (Raylib.IsKeyDown(KeyboardKey.W)) {
                velocityY[c, r] -= Raylib.GetFrameTime() * velocityToAdd.Value;
            }
            if (Raylib.IsKeyDown(KeyboardKey.S)) {
                velocityY[c, r] += Raylib.GetFrameTime() * velocityToAdd.Value;
            } 
            
            if (Raylib.IsKeyDown(KeyboardKey.A)) {
                velocityX[c, r] -= Raylib.GetFrameTime() * velocityToAdd.Value;
            } 
            if (Raylib.IsKeyDown(KeyboardKey.D)) {
                velocityX[c, r] += Raylib.GetFrameTime() * velocityToAdd.Value;
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
        Simulate(fixedUpdateDelta, diffusionRate.Value, viscosityRate.Value);
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

    foreach (Slider s in sliders) s.Draw();

    StringBuilder sb = new();
    if (showHelp) sb.Append(helpText);
        
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

class Slider(string name, float initialValue) {
    public readonly float Min;
    public readonly float Max;

    public Slider(string name, float min, float max, float initialValue) : this(name, initialValue) {
        Min = min;
        Max = max;
    }

    float val = initialValue;
    public float Value { get => val; set => val = float.Clamp(value, Min, Max); }

    Vector2 position, size;
    float sliderWidth;
    float sliderHeight;
    public void SetLayout(Vector2 position, Vector2 size) {
        this.position = position;
        this.size = size;

        sliderWidth = 20.0f;
        sliderHeight = size.Y;
    }

    public void Draw() {
        Rectangle r = new(position, size);

        float offset = (Value - Min) / (Max - Min) * (size.X - sliderWidth);
        
        Rectangle c = new(position.X + offset, position.Y + (size.Y - size.Y) * 0.5f, sliderWidth, sliderHeight);

        Raylib.DrawRectangleRec(r, Color.Blue);
        Raylib.DrawRectangleLinesEx(r, 1.0f, Color.DarkBlue);

        Raylib.DrawRectangleRec(c, Color.Gold);
        Raylib.DrawRectangleLinesEx(c, 1.0f, Color.Orange);

        Raylib.DrawTextEx(Raylib.GetFontDefault(), $"{name}: {Value:F4}", position, size.Y, 1.0f, Color.Black);
    }

    public void HandleMouse() {
        if (Raylib.IsMouseButtonDown(MouseButton.Left)) {
            Vector2 mouse = Raylib.GetMousePosition() - position;
            if (mouse.X >= 0.0f && mouse.X <= size.X && mouse.Y >= 0.0f && mouse.Y <= size.Y) {
                Value = Min + (mouse.X - sliderWidth * 0.5f) / (size.X - sliderWidth) * (Max - Min);
            }
        }
    }
}

