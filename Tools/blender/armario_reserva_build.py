"""Armário de TI FUNCIONAL (reserva de equipamentos) e equipamentos internos do rack de rede.

Gera em Assets/Art/Models (frente em -Y do Blender = +Z local no Unity, como no mobiliario_escritorio_build.py):
  - armario_reserva.fbx  mesmo visual do armario_ti.fbx (1,00 x 0,50 x 1,95 m, 2 portas, venezianas, puxadores),
                         mas OCO e em 3 objetos: Corpo (laterais, fundo, base, teto, 2 prateleiras), Porta_E e Porta_D
                         (origem na dobradiça, para girar). Vãos (altura útil): 0,08–0,66 (gabinete deitado de lado),
                         0,685–1,45 (2 monitores, um atrás do outro) e 1,475–1,92 (livre).
  - rack_interno.fbx     switch 1U, patch panel 1U e organizador de cabos em trilhos 19" com laterais de montagem,
                         dimensionado para o interior do RackInternet (0,47 x 0,48 x 0,54 m). Portas 1–6 visíveis
                         nos dois painéis. Origem no centro da base do interior; frente (portas) em -Y.
Slots: Claro, Escuro, Metal (como no mobiliário) + Porta (latão das portas RJ45) e Etiqueta.

Uso:
  blender -b --python Tools/blender/armario_reserva_build.py -- <pasta .blend> <pasta .fbx>
"""
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mobiliario_escritorio_build as mob  # noqa: E402

SLOTS = ("Claro", "Escuro", "Metal", "Porta", "Etiqueta", "Led", "Cabo")
CORES = dict(mob.CORES, Porta=(0.80, 0.66, 0.30, 1), Etiqueta=(0.95, 0.95, 0.92, 1), Led=(0.1, 0.1, 0.1, 1),
             Cabo=(0.20, 0.45, 0.90, 1))

W, D, H = 1.0, 0.5, 1.95
HX, HY = W / 2, D / 2
PRATELEIRAS = (0.66, 1.45)        # face de baixo de cada prateleira (espessura 0,025)


def mats():
    for n in SLOTS:
        m = bpy.data.materials.get(n) or bpy.data.materials.new(n)
        m.diffuse_color = CORES[n]


def corpo():
    p = mob.Peca("Corpo", SLOTS)
    t = 0.02
    p.caixa(-HX, -HY + 0.02, 0.06, -HX + t, HY, H, "Claro", 0.006)          # lateral esquerda
    p.caixa(HX - t, -HY + 0.02, 0.06, HX, HY, H, "Claro", 0.006)            # lateral direita
    p.caixa(-HX + t, HY - t, 0.06, HX - t, HY, H, "Claro", 0.004)           # fundo
    p.caixa(-HX + t, -HY + 0.02, 0.06, HX - t, HY - t, 0.08, "Claro", 0.004)  # base interna
    p.caixa(-HX + t, -HY + 0.02, H - 0.03, HX - t, HY - t, H, "Claro", 0.004)  # teto
    p.caixa(-HX + 0.02, -HY + 0.03, 0, HX - 0.02, HY - 0.02, 0.06, "Escuro", 0.004)   # rodapé
    p.caixa(-HX - 0.005, -HY + 0.02, H - 0.03, HX + 0.005, HY + 0.005, H + 0.01, "Claro", 0.008)  # tampo
    for z in PRATELEIRAS:
        p.caixa(-HX + t, -HY + 0.03, z, HX - t, HY - t, z + 0.025, "Claro", 0.003)
        p.caixa(-HX + t, -HY + 0.03, z + 0.024, HX - t, -HY + 0.045, z + 0.028, "Escuro", 0)   # friso da frente
    # interior um pouco mais escuro (fundo): faixa de sombra atrás das prateleiras
    p.caixa(-HX + t, HY - t - 0.002, 0.08, HX - t, HY - t, H - 0.03, "Escuro", 0)
    return p.objeto()


def porta(lado):
    """Porta com origem na dobradiça (canto externo, na frente). lado -1 = esquerda, +1 = direita."""
    g = 0.004
    x0, x1 = (-HX + 0.02, -g) if lado < 0 else (g, HX - 0.02)
    hinge_x = x0 if lado < 0 else x1
    p = mob.Peca("Porta_E" if lado < 0 else "Porta_D", SLOTS)

    def cx(a, b, *r):
        p.caixa(a - hinge_x, r[0] + HY, r[1], b - hinge_x, r[2] + HY, r[3], *r[4:])

    cx(x0, x1, -HY, 0.09, -HY + 0.022, H - 0.05, "Claro", 0.006)
    xm = -0.05 if lado < 0 else 0.05                                       # puxador vertical
    cx(xm - 0.012, xm + 0.012, -HY - 0.03, 0.85, -HY - 0.012, 1.15, "Metal", 0.005)
    for z in (0.88, 1.12):
        cx(xm - 0.01, xm + 0.01, -HY - 0.015, z - 0.01, -HY, z + 0.01, "Metal", 0)
    xc = (x0 + x1) / 2
    for k in range(5):
        for zb in (1.62, 0.25):
            z = zb + k * 0.03
            cx(xc - 0.15, xc + 0.15, -HY - 0.004, z, -HY, z + 0.012, "Escuro", 0)
    if lado > 0:                                                           # fechadura e plaqueta (porta direita)
        cx(0.03, 0.06, -HY - 0.006, 1.215, -HY, 1.245, "Metal", 0.004)
    else:
        cx(-0.16 + 0.01, -0.02, -HY - 0.004, 1.74, -HY, 1.83, "Metal", 0.004)
    ob = p.objeto()
    ob.location = (hinge_x, -HY, 0)
    return ob


# ------------------------------------------------------------------ rack

RW, RD, RH = 0.47, 0.48, 0.54          # interior útil do RackInternet
U = 0.0445                             # 1U


def rack_interno():
    p = mob.Peca("Rack_Interno", SLOTS)
    hx, hy = RW / 2, RD / 2
    # trilhos 19" (frente) e traseiros + laterais de montagem
    for sx in (-1, 1):
        for y0, y1 in ((-hy + 0.04, -hy + 0.055), (hy - 0.06, hy - 0.045)):
            p.caixa(sx * hx - (0.02 if sx > 0 else 0), y0, 0.01, sx * hx + (0.02 if sx < 0 else 0), y1, RH - 0.01, "Metal", 0.002)
        p.caixa(sx * hx - (0.004 if sx > 0 else 0), -hy + 0.055, 0.02, sx * hx + (0.004 if sx < 0 else 0), hy - 0.06, 0.04, "Escuro", 0)
        p.caixa(sx * hx - (0.004 if sx > 0 else 0), -hy + 0.055, RH - 0.04, sx * hx + (0.004 if sx < 0 else 0), hy - 0.06, RH - 0.02, "Escuro", 0)
        for k in range(10):                                               # furos dos trilhos
            z = 0.04 + k * 0.048
            p.caixa(sx * (hx - 0.01) - 0.003, -hy + 0.037, z, sx * (hx - 0.01) + 0.003, -hy + 0.04, z + 0.008, "Escuro", 0)
    fx0, fx1 = -hx + 0.02, hx - 0.02     # largura dos equipamentos (entre trilhos)

    def painel(z0, prof, cor):
        p.caixa(fx0, -hy + 0.035, z0, fx1, -hy + 0.035 + prof, z0 + U - 0.002, cor, 0.003)
        for sx in (-1, 1):                                                 # orelhas de montagem
            x = fx0 if sx < 0 else fx1
            p.caixa(x - (0.02 if sx < 0 else 0), -hy + 0.03, z0, x + (0.02 if sx > 0 else 0), -hy + 0.036, z0 + U - 0.002, "Metal", 0.002)

    def portas(z0, n, x_ini, passo, rotulo):
        for k in range(n):
            x = x_ini + k * passo
            p.caixa(x - 0.008, -hy + 0.031, z0 + 0.010, x + 0.008, -hy + 0.036, z0 + 0.024, "Escuro", 0)   # RJ45
            p.caixa(x - 0.006, -hy + 0.0305, z0 + 0.012, x + 0.006, -hy + 0.031, z0 + 0.016, "Porta", 0)   # contatos
            if rotulo:
                p.caixa(x - 0.007, -hy + 0.0305, z0 + 0.029, x + 0.007, -hy + 0.031, z0 + 0.038, "Etiqueta", 0)

    # de cima para baixo: patch panel, organizador, switch, prateleira com o modem
    z_patch = RH - 0.06 - U
    painel(z_patch, 0.08, "Escuro")
    portas(z_patch, 6, -0.125, 0.05, True)
    z_org = z_patch - U
    painel(z_org, 0.06, "Escuro")
    for k in range(5):                                                     # argolas do organizador
        x = -0.15 + k * 0.075
        p.caixa(x - 0.015, -hy + 0.018, z_org + 0.008, x + 0.015, -hy + 0.03, z_org + 0.036, "Metal", 0.003)
    z_sw = z_org - U
    painel(z_sw, 0.22, "Metal")
    p.caixa(fx0 + 0.01, -hy + 0.0305, z_sw + 0.005, fx0 + 0.09, -hy + 0.031, z_sw + U - 0.008, "Escuro", 0)  # visor/marca
    portas(z_sw, 6, -0.125, 0.05, False)
    z_sh = z_sw - 0.13                                                     # prateleira + modem
    p.caixa(fx0, -hy + 0.035, z_sh, fx1, hy - 0.08, z_sh + 0.008, "Metal", 0.002)
    p.caixa(-0.10, -hy + 0.08, z_sh + 0.008, 0.10, -hy + 0.22, z_sh + 0.045, "Claro", 0.006)
    for k in range(4):
        p.caixa(-0.08 + k * 0.03, -hy + 0.0795, z_sh + 0.03, -0.072 + k * 0.03, -hy + 0.08, z_sh + 0.036, "Porta", 0)
    p.caixa(fx0, -hy + 0.035, 0.05, fx1, hy - 0.06, 0.058, "Metal", 0.002)   # bandeja de baixo (nobreak)
    p.caixa(-0.12, -hy + 0.07, 0.058, 0.12, -hy + 0.30, 0.16, "Escuro", 0.008)
    obs = [p.objeto()]
    # LEDs das portas do switch (um objeto por porta: o Unity acende pelo estado da rede)
    for k in range(6):
        x = -0.125 + k * 0.05
        led = mob.Peca(f"Led_S{k + 1}", SLOTS)
        led.caixa(x - 0.004, -hy + 0.029, z_sw + 0.029, x + 0.004, -hy + 0.031, z_sw + 0.036, "Led", 0)
        obs.append(led.objeto())
    # patch cords P n -> S n (um objeto por porta: aparece só com cabo ligado no patch panel), em arco para a frente
    for k in range(6):
        x = -0.125 + k * 0.05
        c = mob.Peca(f"Cabo_{k + 1}", SLOTS)
        za, zb = z_patch + 0.017, z_sw + 0.017
        pts = [(-hy + 0.031, za), (-hy + 0.012, za - 0.004), (-hy - 0.004, za - 0.02), (-hy - 0.012, (za + zb) / 2),
               (-hy - 0.004, zb + 0.02), (-hy + 0.012, zb + 0.004), (-hy + 0.031, zb)]
        r = 0.0028
        for (y0, z0), (y1, z1) in zip(pts, pts[1:]):
            c.caixa(x - r, min(y0, y1) - r, min(z0, z1) - r, x + r, max(y0, y1) + r, max(z0, z1) + r, "Cabo", 0)
        for z in (za, zb):                                                   # plugues
            c.caixa(x - 0.006, -hy + 0.024, z - 0.006, x + 0.006, -hy + 0.034, z + 0.006, "Etiqueta", 0)
        obs.append(c.objeto())
    return obs


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    out_blend_dir, out_fbx_dir = map(os.path.abspath, args[:2])
    os.makedirs(out_blend_dir, exist_ok=True)
    for nome, build in (("armario_reserva", lambda: [corpo(), porta(-1), porta(1)]), ("rack_interno", rack_interno)):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        mats()
        obs = build()
        for ob in obs:
            print("MODELO", nome, ob.name, "dims", tuple(round(d, 3) for d in ob.dimensions), "loc", tuple(round(v, 3) for v in ob.location))
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out_blend_dir, nome + ".blend"))
        bpy.ops.export_scene.fbx(
            filepath=os.path.join(out_fbx_dir, nome + ".fbx"), use_selection=False, object_types={'MESH'},
            apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
            bake_space_transform=True, mesh_smooth_type='FACE', use_tspace=False, add_leaf_bones=False,
            bake_anim=False, path_mode='STRIP')
        print("EXPORTADO", nome)


if __name__ == "__main__":
    main()
