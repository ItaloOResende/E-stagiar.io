"""
Recorta as camadas das peças do gabinete aberto (renders de Tools/blender/pc_interior_renders.py).
Cada camada = só os pixels que a peça muda em relação ao render sem ela (já com a oclusão certa), no mesmo
quadro do fundo; no Canvas elas ficam empilhadas por cima de ui_render_pc_aberto, ocupando o contêiner inteiro.

Uso:  python Tools/blender/pc_interior_camadas.py <pasta_renders> <pasta_saida>
      python Tools/blender/pc_interior_camadas.py <pasta_renders_canvas> <pasta_saida> --led   (LED do Power)
Saída: ui_render_pc_aberto.png (fundo) e ui_render_peca_{placa,ram,cooler,fonte,hd}.png
Imprime 'AREA <peça> xmin ymin xmax ymax' (0–1, origem embaixo à esquerda = âncora do Unity) da parte visível.
"""
import os, sys
from PIL import Image, ImageChops, ImageFilter

src, out = sys.argv[1:3]
os.makedirs(out, exist_ok=True)

if "--led" in sys.argv:
    # LED do botão Power: recorte do que muda entre a frente apagada e a acesa (pasta = renders do Canvas)
    off = Image.open(os.path.join(src, "ui_pc_frente.png")).convert("RGBA")
    on = Image.open(os.path.join(src, "ui_pc_frente_ledon.png")).convert("RGBA")
    m = ImageChops.difference(on, off).convert("L").point(lambda v: 255 if v > 8 else 0).filter(ImageFilter.MaxFilter(5))
    box = m.getbbox()
    led = Image.new("RGBA", on.size, (0, 0, 0, 0)); led.paste(on, (0, 0), m)
    led.crop(box).save(os.path.join(out, "ui_render_pc_frente_led.png"))
    W, H = on.size
    print("AREA led", *(round(v, 4) for v in (box[0] / W, 1 - box[3] / H, box[2] / W, 1 - box[1] / H)))
    sys.exit(0)


def load(n):
    return Image.open(os.path.join(src, n + ".png")).convert("RGBA")


base = load("int_base")
base.save(os.path.join(out, "ui_render_pc_aberto.png"))
W, H = base.size
layers = {  # peça: (render com a peça, render de referência sem ela)
    "placa": ("int_placa", "int_base"),
    "ram": ("int_placa_ram", "int_placa"),
    "cooler": ("int_placa_cooler", "int_placa"),
    "fonte": ("int_fonte", "int_base"),
    "hd": ("int_hd", "int_base"),
}
for name, (with_part, without) in layers.items():
    a, b = load(with_part), load(without)
    diff = ImageChops.difference(a, b).convert("L").point(lambda v: 255 if v > 8 else 0)
    diff = diff.filter(ImageFilter.MaxFilter(3))           # inclui a borda (antisserrilhado/contorno)
    layer = Image.new("RGBA", a.size, (0, 0, 0, 0))
    layer.paste(a, (0, 0), diff)
    layer.save(os.path.join(out, f"ui_render_peca_{name}.png"))
    x0, y0, x1, y1 = diff.getbbox()
    print("AREA", name, *(round(v, 4) for v in (x0 / W, 1 - y1 / H, x1 / W, 1 - y0 / H)))
print("QUADRO", W, H)
