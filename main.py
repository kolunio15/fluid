from pyray import *

width = 32
width_with_border = width + 2
height = 32
height_with_border = height + 2

# [column, row]
density = []
velocity = []
for i in range(width):
    density.append([0] * height)
    velocity.append([Vector2(0, 0)] * height)


display_width = 512
display_height = 512

init_window(800, 800, "Hello")
while not window_should_close():
    begin_drawing()
    clear_background(WHITE)
    draw_text("Hello world", 190, 200, 20, VIOLET)

    cell_size = Vector2(display_width / width_with_border, display_height / height_with_border)
    for r in range(width_with_border):
        for c in range(height_with_border):
            offset = Vector2(0, 0)
            rect = Rectangle(offset.x + cell_size.x * c, offset.y + cell_size.y * r, cell_size.x, cell_size.y)
            draw_rectangle_rec(rect, RED)
            draw_rectangle_lines_ex(rect, 1.0, BLACK)

    end_drawing()
close_window()