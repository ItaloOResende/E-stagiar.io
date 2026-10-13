"""
Componentes internos do PC em 3D simples (autoria própria do projeto), em metros reais, no estilo dos modelos
do gabinete (formas simples, cores chapadas, pequenos detalhes): placa-mãe, memória RAM, HD 3,5", fonte ATX e
cooler do processador. Usado por Tools/blender/pc_interior_renders.py (gabinete aberto do Canvas) e exportado
como itens do mundo (componentes.fbx).

Funções: cada uma devolve UM objeto (já juntado) em "coordenadas da peça":
  placa_mae(): placa no plano XY, componentes para +Z; X = da traseira (I/O, x<0) para a frente; Y = para cima.
               O layout segue o sprite placa_mae.png (processador em cima à esquerda, 2 slots de RAM à direita dele).
  memoria_ram(): pente de pé: comprimento em Y, altura em Z (contatos dourados em z = 0), face em X.
  cooler(), hd(), fonte(): base em z = 0, centrados em X/Y.
  pendrive(): "coordenadas de porta", como os plugues de cabos.fbx: entra na USB VERTICAL da traseira do gabinete
              (boca 9 x 16 mm, gabinete_v2_build.usb_t) indo para -Y; a boca da porta fica em y = 0, o conector
              metálico ocupa y < 0 (dentro da porta) e o corpo fica em y > 0 (fora do gabinete).
  chave_de_fenda(): chave de fenda (Phillips) deitada ao longo de X, ponta em -X, eixo em z = 0.
  quebrada(o, ...): versão QUEBRADA de um item deitado (placa-mãe, RAM): a própria malha cortada em diagonal,
              uma metade afastada e entortada (dobrada para cima) e algumas lascas soltas; exportada como
              Item_PlacaMae_Quebrada / Item_MemoriaRAM_Quebrada / Item_HD_Quebrado / Item_Fonte_Quebrada /
              Item_Cooler_Quebrado (mesma peça, reconhecível, danificada).
Constantes de encaixe: CPU_XY (centro do soquete), RAM_XY (centro do 1º slot), na placa.

Uso (headless, exporta os itens deitados):
  blender -b --factory-startup --python Tools/blender/componentes_build.py -- <saida.blend> <saida.fbx> [<pasta_renders>]
  <pasta_renders>: ui_render_pendrive.png (pendrive encaixado visto de trás do gabinete, para o Canvas) e
                   ui_cursor_chave_de_fenda.png (chave de fenda em ângulo fixo, ponta embaixo à esquerda: cursor da
                   Traseira; imprime 'HOTSPOT x y' em pixels da imagem, origem no canto superior esquerdo).
"""
import bpy, math, os, sys
from mathutils import Vector, Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gabinete_v2_build as v2  # noqa: E402

MB = 0.244                       # micro-ATX 24,4 cm
CPU_XY = (-0.122 + 0.29 * MB, 0.122 - 0.275 * MB)    # soquete (fx 0,14–0,44 / fy 0,11–0,44 no sprite)
RAM_XY = (-0.122 + 0.575 * MB, 0.122 - 0.28 * MB)    # 1º slot (fx 0,546–0,64)
M = {}


def mats():
    v2.mats()
    M.update(v2.MAT)
    M["pcb"] = v2.material("MAT_Comp_PCB", (0.10, 0.42, 0.20), rough=0.6)
    M["pcb_ram"] = v2.material("MAT_Comp_PCB_RAM", (0.08, 0.36, 0.17), rough=0.6)
    M["chip"] = v2.material("MAT_Comp_Chip", (0.09, 0.09, 0.10), rough=0.5)
    M["slot"] = v2.material("MAT_Comp_Slot", (0.14, 0.14, 0.16), rough=0.5)
    M["branco"] = v2.material("MAT_Comp_Branco", (0.90, 0.90, 0.86), rough=0.5)
    M["dourado"] = v2.material("MAT_Comp_Dourado", (0.95, 0.75, 0.25), metallic=0.6, rough=0.3)
    M["aluminio"] = v2.material("MAT_Comp_Aluminio", (0.76, 0.78, 0.82), metallic=0.5, rough=0.35)
    M["etiqueta"] = v2.material("MAT_Comp_Etiqueta", (0.95, 0.93, 0.80), rough=0.6)
    M["fan"] = v2.material("MAT_Comp_Ventoinha", (0.30, 0.58, 0.86), rough=0.45)
    M["vermelho"] = v2.material("MAT_Comp_Vermelho", (0.84, 0.27, 0.21), rough=0.5)


def box(size, loc, mat, bevel=0.0):
    return v2.box("b", size, loc, mat, bevel=bevel, segments=2)


def cyl(r, h, loc, mat, axis='Z', verts=16, bevel=0.0):
    return v2.cyl("c", r, h, loc, axis, mat, verts=verts, bevel=bevel)


def done(parts, name):
    o = v2.join(parts, name)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    o.select_set(False)
    return o


def placa_mae():
    t = 0.0016
    p = [box((MB, MB, t), (0, 0, t / 2), M["pcb"], bevel=0.0005)]
    z = t
    cx, cy = CPU_XY
    p.append(box((0.058, 0.058, 0.004), (cx, cy, z + 0.002), M["slot"], bevel=0.001))          # soquete
    p.append(box((0.046, 0.046, 0.0045), (cx, cy, z + 0.0025), M["aluminio"], bevel=0.001))
    for i in range(10):                                                                     # pinos dourados em volta
        d = -0.025 + i * 0.0055
        for (x, y) in ((cx + d, cy + 0.031), (cx + d, cy - 0.031), (cx + 0.031, cy + d), (cx - 0.031, cy + d)):
            p.append(box((0.0025, 0.0025, 0.0015), (x, y, z + 0.0008), M["dourado"]))
    rx, ry = RAM_XY
    for k in range(2):                                                                      # 2 slots de RAM (verticais)
        x = rx + k * 0.012
        p.append(box((0.0065, 0.140, 0.008), (x, ry, z + 0.004), M["slot"], bevel=0.0008))
        for s in (-1, 1):
            p.append(box((0.0068, 0.006, 0.010), (x, ry + s * 0.072, z + 0.005), M["branco"]))  # travas
    p.append(box((0.040, 0.040, 0.010), (-0.040, -0.035, z + 0.005), M["aluminio"], bevel=0.002))  # chipset
    for i in range(4):
        p.append(box((0.036, 0.002, 0.004), (-0.040, -0.050 + i * 0.010, z + 0.012), M["slot"]))
    for i, y in enumerate((-0.060, -0.085)):                                                # slots PCIe
        p.append(box((0.150, 0.007, 0.010), (-0.005, y, z + 0.005), M["slot"], bevel=0.0008))
    p.append(box((0.020, 0.052, 0.010), (0.106, 0.010, z + 0.005), M["branco"], bevel=0.001))  # ATX 24 pinos (frente)
    p.append(box((0.022, 0.150, 0.026), (-0.111, 0.040, z + 0.013), M["aluminio"], bevel=0.002))  # I/O traseiro
    for i, y in enumerate((0.095, 0.075, 0.055, 0.020)):
        p.append(box((0.006, 0.014, 0.012), (-0.123, y, z + 0.014), M["vga"] if i == 3 else M["chip"]))
    for (x, y) in ((-0.075, 0.105), (-0.065, 0.105), (-0.075, -0.01), (0.03, 0.105), (0.04, 0.105), (0.05, 0.105)):
        p.append(cyl(0.0035, 0.009, (x, y, z + 0.0045), M["chip"], verts=10))               # capacitores
    p.append(box((0.016, 0.016, 0.003), (0.070, -0.030, z + 0.0015), M["chip"]))           # chips soltos
    p.append(box((0.012, 0.025, 0.003), (0.070, -0.075, z + 0.0015), M["chip"]))
    for i in range(4):                                                                      # conectores SATA
        p.append(box((0.006, 0.012, 0.006), (0.108, -0.045 - i * 0.015, z + 0.003), M["slot"]))
    p.append(cyl(0.009, 0.003, (0.050, 0.020, z + 0.0015), M["parafuso"], verts=14))       # bateria CMOS
    for (x, y) in ((-0.110, 0.110), (0.110, 0.110), (-0.110, -0.110), (0.110, -0.110), (0.0, 0.110), (0.0, -0.110)):
        p.append(cyl(0.0035, 0.0012, (x, y, z + 0.0006), M["parafuso"], verts=10))          # furos de fixação
    return done(p, "Comp_PlacaMae")


def memoria_ram():
    L, Hh, t = 0.133, 0.031, 0.0012
    p = [box((t, L, Hh), (0, 0, Hh / 2), M["pcb_ram"], bevel=0.0003)]
    for s in (-1, 1):
        for i in range(8):
            y = -L / 2 + 0.010 + i * (L - 0.020) / 7
            p.append(box((0.0012, 0.011, 0.011), (s * 0.0011, y, Hh * 0.55), M["chip"]))
        p.append(box((0.0006, L * 0.96, 0.0035), (s * 0.0008, 0, 0.0022), M["dourado"]))     # contatos
        p.append(box((0.0005, 0.040, 0.006), (s * 0.0009, -0.035, Hh - 0.006), M["etiqueta"]))
    p.append(box((t + 0.0003, 0.004, 0.0045), (0, 0.010, 0.0022), M["pcb_ram"]))            # entalhe (marcador)
    return done(p, "Comp_MemoriaRAM")


def cooler():
    s, hb = 0.090, 0.030
    p = [box((s, s, 0.004), (0, 0, 0.002), M["aluminio"], bevel=0.001)]
    for i in range(13):                                                                     # aletas
        x = -s / 2 + 0.004 + i * (s - 0.008) / 12
        p.append(box((0.0014, s - 0.004, hb), (x, 0, 0.004 + hb / 2), M["aluminio"]))
    z = 0.004 + hb
    p.append(box((s, s, 0.004), (0, 0, z + 0.002), M["chip"], bevel=0.002))                # moldura da ventoinha
    p.append(cyl(0.040, 0.012, (0, 0, z + 0.010), M["chip"], verts=32, bevel=0.002))
    for k in range(7):                                                                      # pás
        a = k * 2 * math.pi / 7
        b = box((0.030, 0.011, 0.002), (0.019 * math.cos(a), 0.019 * math.sin(a), z + 0.014), M["fan"])
        b.rotation_euler = (math.radians(18), 0, a)
        p.append(b)
    p.append(cyl(0.011, 0.006, (0, 0, z + 0.014), M["aluminio"], verts=20))
    p.append(cyl(0.004, 0.0015, (0, 0, z + 0.0175), M["vermelho"], verts=12))
    for x, y in ((-0.040, -0.040), (0.040, -0.040), (-0.040, 0.040), (0.040, 0.040)):
        p.append(cyl(0.0035, 0.004, (x, y, z + 0.006), M["parafuso"], verts=10))
    return done(p, "Comp_Cooler")


def hd():
    w, d, h = 0.102, 0.147, 0.026
    p = [box((w, d, h * 0.8), (0, 0, h * 0.4 + h * 0.2), M["aluminio"], bevel=0.003)]
    p.append(box((w * 0.94, d * 0.96, h * 0.2), (0, 0, h * 0.1), M["pcb"], bevel=0.001))        # placa por baixo
    p.append(box((0.060, 0.070, 0.0006), (-0.004, 0.020, h + 0.0003), M["etiqueta"]))
    p.append(box((0.060, 0.012, 0.0007), (-0.004, 0.048, h + 0.0004), M["vga"]))
    p.append(cyl(0.032, 0.0015, (0.010, -0.030, h + 0.0005), M["aluminio"], verts=32))       # tampa do motor
    for x, y in ((-0.045, -0.068), (0.045, -0.068), (-0.045, 0.068), (0.045, 0.068)):
        p.append(cyl(0.0025, 0.0012, (x, y, h + 0.0006), M["parafuso"], verts=8))
    p.append(box((0.040, 0.004, 0.010), (0.010, -d / 2 - 0.001, 0.008), M["chip"]))         # conectores SATA
    return done(p, "Comp_HD")


def fonte():
    w, d, h = 0.150, 0.140, 0.086
    p = [box((w, d, h), (0, 0, h / 2), M["chapa"], bevel=0.003)]
    p.append(box((0.080, 0.055, 0.0008), (0.010, 0.020, h + 0.0004), M["etiqueta"]))        # etiqueta em cima
    p.append(box((0.080, 0.010, 0.0009), (0.010, 0.042, h + 0.0005), M["vermelho"]))
    p.append(cyl(0.050, 0.0012, (0.0, -0.020, h + 0.0006), M["chip"], verts=32))            # grade da ventoinha
    for r in (0.020, 0.035, 0.048):
        p.append(cyl(r, 0.0016, (0.0, -0.020, h + 0.0008), M["chapa"], verts=32))
        p.append(cyl(r - 0.003, 0.0018, (0.0, -0.020, h + 0.0009), M["chip"], verts=32))
    p.append(box((0.0008, 0.090, 0.050), (w / 2 + 0.0004, 0.0, h / 2), M["etiqueta"]))          # etiqueta lateral
    p.append(box((0.0009, 0.090, 0.009), (w / 2 + 0.0005, 0.0, h / 2 + 0.020), M["vermelho"]))
    p.append(box((0.040, 0.025, 0.030), (0.040, d / 2 + 0.010, 0.030), M["chip"], bevel=0.004))  # saída dos cabos
    for i, c in enumerate(("vermelho", "etiqueta", "chip", "etiqueta", "vermelho")):
        p.append(cyl(0.0022, 0.030, (0.030 + i * 0.005, d / 2 + 0.030, 0.030), M[c], axis='Y', verts=8))
    return done(p, "Comp_Fonte")


USB_BOCA = (0.009, 0.016)        # largura (X) x altura (Z) da USB do gabinete (gabinete_v2_build: usb_t)


def pendrive():
    bw, bh = USB_BOCA
    sw, sh, sl = bw - 0.0012, bh - 0.0014, 0.012                                             # conector USB-A (cabe na boca)
    p = [box((sw, sl, sh), (0, -sl / 2 + 0.001, 0), M["aluminio"], bevel=0.0004)]
    for z in (-0.003, 0.003):                                                                # janelinhas da casca
        p.append(box((sw + 0.0002, 0.0022, 0.0022), (0, -0.007, z), M["chip"]))
    L, w, h = 0.044, 0.010, 0.020                                                            # corpo
    p.append(box((w, L, h), (0, L / 2 + 0.001, 0), M["fan"], bevel=0.0025))
    p.append(box((w + 0.0003, 0.018, h * 0.62), (0, 0.020, 0), M["etiqueta"]))              # etiqueta (dos dois lados)
    p.append(box((w + 0.0004, 0.004, h * 0.62), (0, 0.009, 0), M["vermelho"]))
    p.append(cyl(0.0032, w + 0.0006, (0, L - 0.004, 0), M["chip"], axis='X', verts=14))      # furo do chaveiro
    p.append(box((0.0016, 0.003, 0.003), (w / 2 + 0.0002, 0.033, h * 0.30), M["vermelho"]))  # LED de atividade
    return done(p, "Comp_Pendrive")


def chave_de_fenda():
    Lc, rc = 0.095, 0.0135                                                                   # cabo
    Lh, rh = 0.105, 0.0030                                                                   # haste
    p = [cyl(rc, Lc, (Lc / 2, 0, 0), M["vermelho"], axis='X', verts=8, bevel=0.004)]         # cabo octogonal (pega)
    for x in (0.020, 0.048, 0.076):                                                          # frisos pretos da pega
        p.append(cyl(rc + 0.0006, 0.006, (x, 0, 0), M["chip"], axis='X', verts=8))
    p.append(cyl(rc * 0.62, 0.012, (-0.004, 0, 0), M["chip"], axis='X', verts=16, bevel=0.002))  # virola
    p.append(cyl(rh, Lh, (-0.010 - Lh / 2, 0, 0), M["aluminio"], axis='X', verts=12))        # haste
    tip = cyl(rh, 0.012, (-0.010 - Lh - 0.004, 0, 0), M["aluminio"], axis='X', verts=4)      # ponta Phillips
    tip.scale = (1.0, 1.0, 1.0)
    p.append(tip)
    p.append(box((0.010, rh * 2.6, 0.0012), (-0.010 - Lh - 0.005, 0, 0), M["aluminio"]))     # cruz da ponta
    p.append(box((0.010, 0.0012, rh * 2.6), (-0.010 - Lh - 0.005, 0, 0), M["aluminio"]))
    return done(p, "Comp_ChaveDeFenda")


def quebrada(src, nome, normal, gap, tilt_deg, lascas):
    """Corta 'src' (item deitado, base em z = 0) por um plano vertical pelo centro, separa as metades e dobra uma."""
    import bmesh
    vs = [src.matrix_world @ v.co for v in src.data.vertices]
    c = Vector(((min(v.x for v in vs) + max(v.x for v in vs)) / 2, (min(v.y for v in vs) + max(v.y for v in vs)) / 2, 0))
    n = Vector(normal).normalized()
    halves = []
    for keep_outer in (False, True):
        o = src.copy(); o.data = src.data.copy(); bpy.context.scene.collection.objects.link(o)
        bm = bmesh.new(); bm.from_mesh(o.data)
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        r = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=c, plane_no=n, clear_inner=keep_outer, clear_outer=not keep_outer)
        edges = [e for e in r["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
        bmesh.ops.holes_fill(bm, edges=edges, sides=0)                     # tampa o corte (borda da placa partida)
        bm.to_mesh(o.data); bm.free()
        halves.append(o)
    a, b = halves
    axis = Vector((-n.y, n.x, 0)).normalized()                            # linha do corte
    m = (Matrix.Translation(c + n * gap) @ Matrix.Rotation(math.radians(tilt_deg), 4, axis) @ Matrix.Translation(-c))
    b.matrix_world = m @ b.matrix_world                                   # metade de fora afastada e dobrada
    parts = [a, b]
    mat = src.data.materials[0]
    for (dx, dy, sx, sy, rot) in lascas:                                  # lascas da placa soltas perto do corte
        l = v2.box("lasca", (sx, sy, 0.0016), (c.x + dx, c.y + dy, 0.0008), mat)
        l.rotation_euler = (0, 0, math.radians(rot))
        parts.append(l)
    o = done(parts, nome)
    zmin = min((o.matrix_world @ v.co).z for v in o.data.vertices)
    for v in o.data.vertices:
        v.co.z -= zmin
    return o


def build_all():
    mats()
    return {"PlacaMae": placa_mae(), "MemoriaRAM": memoria_ram(), "Cooler": cooler(), "HD": hd(), "Fonte": fonte(),
            "Pendrive": pendrive(), "ChaveDeFenda": chave_de_fenda()}


if __name__ == "__main__" and "--" in sys.argv and os.path.basename(sys.argv[sys.argv.index("--") - 1]).startswith("componentes_build"):
    args = sys.argv[sys.argv.index("--") + 1:]
    out_blend, out_fbx = map(os.path.abspath, args[:2])
    renders = os.path.abspath(args[2]) if len(args) > 2 else None
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objs = build_all()
    if renders:                                     # pendrive encaixado, visto de trás do gabinete (+Y), em escala real
        import cabos_build as cb                    # noqa: E402  (mesmo render dos plugues do Canvas)
        cb.render_setup()
        os.makedirs(renders, exist_ok=True)
        cb.render([objs["Pendrive"]], os.path.join(renders, "ui_render_pendrive.png"), from_back=True)
        # cursor: chave de fenda inclinada 45 graus, ponta embaixo à esquerda (vista de frente, -Y)
        ch = objs["ChaveDeFenda"]
        ch.matrix_world = Matrix.Rotation(math.radians(-45), 4, 'Y') @ ch.matrix_world
        bpy.context.view_layer.update()
        vs = [ch.matrix_world @ v.co for v in ch.data.vertices]
        x0, x1 = min(v.x for v in vs), max(v.x for v in vs)
        z0, z1 = min(v.z for v in vs), max(v.z for v in vs)
        pad = 0.004
        frame = (x0 - pad, z0 - pad, x1 + pad, z1 + pad)
        out = os.path.join(renders, "ui_cursor_chave_de_fenda.png")
        cb.render([ch], out, from_back=False, frame=frame, height=256)
        tipv = min(vs, key=lambda v: v.x + v.z)                     # ponta = vértice mais embaixo à esquerda
        w, h = frame[2] - frame[0], frame[3] - frame[1]
        rx, ry = bpy.context.scene.render.resolution_x, bpy.context.scene.render.resolution_y
        print("HOTSPOT", round((tipv.x - frame[0]) / w * rx), round((frame[3] - tipv.z) / h * ry))
        ch.matrix_world = Matrix.Rotation(math.radians(45), 4, 'Y') @ ch.matrix_world
        for o in bpy.data.objects:
            o.hide_render = False
    # pendrive deitado (face maior para baixo): espessura (X) vira a altura
    pd = objs["Pendrive"]
    pd.matrix_world = Matrix.Rotation(math.radians(90), 4, 'Y') @ pd.matrix_world
    bpy.ops.object.select_all(action='DESELECT'); pd.select_set(True); bpy.context.view_layer.objects.active = pd
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    # itens deitados: RAM de pé vira deitada (face para cima); demais já estão deitados; base em z = 0
    ram = objs["MemoriaRAM"]
    ram.matrix_world = Matrix.Rotation(math.radians(90), 4, 'Z') @ Matrix.Rotation(math.radians(90), 4, 'Y') @ ram.matrix_world  # comprimento em X
    bpy.ops.object.select_all(action='DESELECT'); ram.select_set(True); bpy.context.view_layer.objects.active = ram
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    # versões quebradas (depois da RAM deitada, para cortar já na posição de item)
    objs["PlacaMae_Quebrada"] = quebrada(objs["PlacaMae"], "Comp_PlacaMae_Quebrada", (1.0, 0.45, 0), 0.010, 9,
                                         [(0.03, -0.09, 0.014, 0.009, 20), (-0.02, 0.10, 0.010, 0.012, -35), (0.05, 0.07, 0.008, 0.006, 60)])
    objs["MemoriaRAM_Quebrada"] = quebrada(objs["MemoriaRAM"], "Comp_MemoriaRAM_Quebrada", (1.0, 0.30, 0), 0.006, 14,
                                           [(0.012, -0.024, 0.006, 0.004, 30), (-0.010, 0.022, 0.005, 0.004, -20)])
    # resistentes (quebram só na 2ª queda): carcaça partida, uma metade afastada e inclinada
    objs["HD_Quebrado"] = quebrada(objs["HD"], "Comp_HD_Quebrado", (1.0, 0.55, 0), 0.008, 7,
                                   [(0.02, -0.03, 0.008, 0.006, 25), (-0.015, 0.035, 0.006, 0.005, -40)])
    objs["Fonte_Quebrada"] = quebrada(objs["Fonte"], "Comp_Fonte_Quebrada", (1.0, -0.4, 0), 0.010, 6,
                                      [(0.03, 0.04, 0.010, 0.007, 15), (-0.02, -0.05, 0.008, 0.006, -30)])
    objs["Cooler_Quebrado"] = quebrada(objs["Cooler"], "Comp_Cooler_Quebrado", (1.0, 0.8, 0), 0.007, 10,
                                       [(0.015, -0.02, 0.006, 0.005, 35), (-0.02, 0.01, 0.005, 0.004, -15)])
    for name, o in objs.items():
        vs = [v.co for v in o.data.vertices]
        mn = Vector([min(v[i] for v in vs) for i in range(3)]); mx = Vector([max(v[i] for v in vs) for i in range(3)])
        c = (mn + mx) / 2
        for v in o.data.vertices:
            v.co -= Vector((c.x, c.y, mn.z))
        o.name = o.data.name = "Item_" + name
        print("ITEM", o.name, "tamanho", tuple(round(x, 3) for x in (mx - mn)))
    os.makedirs(os.path.dirname(out_blend), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=out_blend)
    bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=False, object_types={'MESH'}, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             mesh_smooth_type='FACE', use_tspace=False, add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
    print("EXPORTADO", out_fbx)
