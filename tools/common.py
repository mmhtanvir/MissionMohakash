from glbkit import *

# ---- shared palette (sRGB values; the writer converts to linear) ----
HULL      = Mat('HullWhite',      (0.86, 0.87, 0.88), 0.10, 0.55)
HULL_GRAY = Mat('HullPanelGray',  (0.52, 0.55, 0.60), 0.35, 0.50)
DARK      = Mat('DarkMetal',      (0.13, 0.14, 0.16), 0.80, 0.40)
STEEL     = Mat('Steel',          (0.66, 0.68, 0.72), 0.90, 0.32)
ORANGE    = Mat('SafetyOrange',   (0.95, 0.38, 0.05), 0.00, 0.50)
YELLOW    = Mat('HazardYellow',   (0.96, 0.78, 0.08), 0.00, 0.50)
RED       = Mat('SignalRed',      (0.80, 0.08, 0.06), 0.00, 0.50)
GREEN_BD  = Mat('FlagGreen',      (0.00, 0.42, 0.31), 0.00, 0.60)
RUBBER    = Mat('Rubber',         (0.06, 0.06, 0.07), 0.00, 0.90)
SOLAR     = Mat('SolarCell',      (0.05, 0.11, 0.32), 0.60, 0.22)
SOLAR_LN  = Mat('SolarGrid',      (0.55, 0.62, 0.78), 0.90, 0.30)
GOLD_FOIL = Mat('GoldFoil',       (0.85, 0.62, 0.16), 1.00, 0.28)
VISOR     = Mat('VisorGold',      (0.85, 0.66, 0.22), 1.00, 0.08)
GLASS     = Mat('GlassBlue',      (0.30, 0.55, 0.75), 0.00, 0.10, alpha=0.40, double=True)
SUIT      = Mat('SuitWhite',      (0.93, 0.93, 0.91), 0.00, 0.65)
SUIT_GRAY = Mat('SuitGray',       (0.45, 0.47, 0.50), 0.20, 0.55)
GLOVE     = Mat('GloveGray',      (0.28, 0.30, 0.33), 0.10, 0.70)
MIL_GREEN = Mat('CrateOlive',     (0.24, 0.30, 0.20), 0.10, 0.65)
O2_BLUE   = Mat('OxygenBlue',     (0.12, 0.42, 0.92), 0.30, 0.40)
H2O_TEAL  = Mat('WaterTeal',      (0.00, 0.68, 0.78), 0.30, 0.40)
TANK_BODY = Mat('TankShell',      (0.82, 0.86, 0.90), 0.55, 0.35)
CONCRETE  = Mat('PadPlate',       (0.38, 0.39, 0.41), 0.20, 0.80)

# emissive lights (estr>1 so URP bloom can pick them up)
LED_GREEN = Mat('LED_Green', (0.1, 1.0, 0.3), 0.0, 0.4, emissive=(0.1, 1.0, 0.3), estr=3.0)
LED_RED   = Mat('LED_Red',   (1.0, 0.1, 0.05), 0.0, 0.4, emissive=(1.0, 0.1, 0.05), estr=3.0)
LED_BLUE  = Mat('LED_Blue',  (0.2, 0.55, 1.0), 0.0, 0.4, emissive=(0.2, 0.55, 1.0), estr=3.0)
LED_AMBER = Mat('LED_Amber', (1.0, 0.65, 0.05), 0.0, 0.4, emissive=(1.0, 0.65, 0.05), estr=3.0)
LAMP      = Mat('LampWhite', (1.0, 0.97, 0.88), 0.0, 0.4, emissive=(1.0, 0.95, 0.85), estr=4.0)
SCREEN    = Mat('ScreenCyan', (0.1, 0.8, 0.9), 0.0, 0.3, emissive=(0.1, 0.8, 0.9), estr=2.0)

def blink_anim(name, nodes_times_values):
    pass
