from __future__ import annotations

import math
import random
from pathlib import Path
from typing import Iterable, Sequence

from PIL import Image, ImageChops, ImageDraw, ImageFilter


OUTPUT_SIZE = (2048, 3072)
AA_SCALE = 2
CANVAS_SIZE = (OUTPUT_SIZE[0] * AA_SCALE, OUTPUT_SIZE[1] * AA_SCALE)

ROOT = Path(__file__).resolve().parents[1]
OUTPUT_DIR = ROOT / "Assets" / "Gathering" / "InspectionPlants"


Point = tuple[float, float]
Color = tuple[int, int, int, int]


def px(value: float) -> float:
    return value * AA_SCALE


def p(x: float, y: float) -> Point:
    return (x * CANVAS_SIZE[0], y * CANVAS_SIZE[1])


def add(a: Point, b: Point) -> Point:
    return (a[0] + b[0], a[1] + b[1])


def mul(a: Point, scalar: float) -> Point:
    return (a[0] * scalar, a[1] * scalar)


def unit(angle_degrees: float) -> Point:
    radians = math.radians(angle_degrees)
    return (math.cos(radians), math.sin(radians))


def normal(direction: Point) -> Point:
    return (-direction[1], direction[0])


def clamp_channel(value: int) -> int:
    return max(0, min(255, value))


def shift_color(color: Color, r: int, g: int, b: int, a: int = 0) -> Color:
    return (
        clamp_channel(color[0] + r),
        clamp_channel(color[1] + g),
        clamp_channel(color[2] + b),
        clamp_channel(color[3] + a),
    )


def jitter_color(rng: random.Random, color: Color, amount: int) -> Color:
    return (
        clamp_channel(color[0] + rng.randint(-amount, amount)),
        clamp_channel(color[1] + rng.randint(-amount, amount)),
        clamp_channel(color[2] + rng.randint(-amount, amount)),
        color[3],
    )


def int_points(points: Iterable[Point]) -> list[tuple[int, int]]:
    return [(round(x), round(y)) for x, y in points]


def bezier_point(p0: Point, p1: Point, p2: Point, p3: Point, t: float) -> Point:
    u = 1.0 - t
    return (
        (u * u * u * p0[0]) + (3.0 * u * u * t * p1[0]) + (3.0 * u * t * t * p2[0]) + (t * t * t * p3[0]),
        (u * u * u * p0[1]) + (3.0 * u * u * t * p1[1]) + (3.0 * u * t * t * p2[1]) + (t * t * t * p3[1]),
    )


def bezier_points(p0: Point, p1: Point, p2: Point, p3: Point, count: int = 96) -> list[Point]:
    return [bezier_point(p0, p1, p2, p3, index / (count - 1)) for index in range(count)]


def point_on_polyline(points: Sequence[Point], t: float) -> Point:
    if not points:
        return (0.0, 0.0)

    if t <= 0.0:
        return points[0]
    if t >= 1.0:
        return points[-1]

    lengths: list[float] = []
    total = 0.0
    for index in range(len(points) - 1):
        length = math.dist(points[index], points[index + 1])
        lengths.append(length)
        total += length

    if total <= 0.0:
        return points[0]

    remaining = total * t
    for index, length in enumerate(lengths):
        if remaining > length:
            remaining -= length
            continue

        segment_t = remaining / length if length > 0.0 else 0.0
        start = points[index]
        end = points[index + 1]
        return (
            start[0] + ((end[0] - start[0]) * segment_t),
            start[1] + ((end[1] - start[1]) * segment_t),
        )

    return points[-1]


def polyline_tangent(points: Sequence[Point], t: float) -> Point:
    if len(points) < 2:
        return (0.0, -1.0)

    a = point_on_polyline(points, max(0.0, t - 0.01))
    b = point_on_polyline(points, min(1.0, t + 0.01))
    dx = b[0] - a[0]
    dy = b[1] - a[1]
    length = math.hypot(dx, dy)
    if length <= 0.0:
        return (0.0, -1.0)

    return (dx / length, dy / length)


def draw_polyline(
    image: Image.Image,
    points: Sequence[Point],
    color: Color,
    width: float,
    *,
    shadow: bool = True,
    highlight: bool = True,
) -> None:
    draw = ImageDraw.Draw(image, "RGBA")
    width_i = max(1, round(width))
    rounded_points = int_points(points)

    if shadow:
        offset = round(px(6))
        draw.line(
            [(x + offset, y + offset) for x, y in rounded_points],
            fill=(0, 0, 0, 46),
            width=width_i + round(px(5)),
            joint="curve",
        )

    draw.line(rounded_points, fill=shift_color(color, -34, -30, -28, 0), width=width_i + round(px(4)), joint="curve")
    draw.line(rounded_points, fill=color, width=width_i, joint="curve")

    if highlight and width_i > 3:
        draw.line(
            [(x - round(width * 0.12), y) for x, y in rounded_points],
            fill=shift_color(color, 44, 48, 28, -70),
            width=max(1, round(width * 0.25)),
            joint="curve",
        )


def polygon_bounds(points: Sequence[Point], margin: float) -> tuple[int, int, int, int]:
    xs = [point[0] for point in points]
    ys = [point[1] for point in points]
    left = max(0, math.floor(min(xs) - margin))
    top = max(0, math.floor(min(ys) - margin))
    right = min(CANVAS_SIZE[0], math.ceil(max(xs) + margin))
    bottom = min(CANVAS_SIZE[1], math.ceil(max(ys) + margin))
    return left, top, right, bottom


def masked_alpha_composite(
    image: Image.Image,
    layer: Image.Image,
    mask: Image.Image,
    offset: tuple[int, int],
) -> None:
    channels = list(layer.split())
    channels[3] = ImageChops.multiply(channels[3], mask)
    layer.putalpha(channels[3])
    image.alpha_composite(layer, offset)


def leaf_midpoint(base: Point, direction: Point, leaf_length: float, curve: float, t: float) -> Point:
    side = normal(direction)
    return add(add(base, mul(direction, leaf_length * t)), mul(side, math.sin(math.pi * t) * leaf_length * curve))


def leaf_half_width(leaf_width: float, t: float, roundness: float) -> float:
    if t <= 0.0 or t >= 1.0:
        return 0.0

    shape = math.sin(math.pi * t)
    taper = 0.78 + (0.22 * (1.0 - t))
    return leaf_width * (shape ** roundness) * taper


def build_leaf_points(
    base: Point,
    angle_degrees: float,
    leaf_length: float,
    leaf_width: float,
    *,
    serrated: bool,
    roundness: float,
    curve: float,
    serration_amount: float = 0.11,
) -> tuple[list[Point], list[Point], list[Point], Point, Point]:
    direction = unit(angle_degrees)
    side = normal(direction)
    left_edge: list[Point] = []
    right_edge: list[Point] = []
    steps = 38 if serrated else 30

    for index in range(steps + 1):
        t = index / steps
        center = leaf_midpoint(base, direction, leaf_length, curve, t)
        width = leaf_half_width(leaf_width, t, roundness)
        if serrated and 0.08 < t < 0.94:
            width *= 1.0 + (serration_amount if index % 2 == 0 else -serration_amount * 0.7)

        left_edge.append(add(center, mul(side, width)))
        right_edge.append(add(center, mul(side, -width)))

    polygon = left_edge + list(reversed(right_edge))
    tip = add(base, mul(direction, leaf_length))
    return polygon, left_edge, right_edge, direction, tip


def draw_leaf(
    image: Image.Image,
    rng: random.Random,
    attach: Point,
    angle_degrees: float,
    leaf_length: float,
    leaf_width: float,
    fill: Color,
    *,
    serrated: bool = True,
    vein_style: str = "net",
    petiole_length: float = 42.0,
    petiole_width: float = 7.0,
    roundness: float = 0.78,
    curve: float = 0.02,
    serration_amount: float = 0.11,
) -> None:
    direction = unit(angle_degrees)
    side = normal(direction)
    leaf_base = add(attach, mul(direction, petiole_length))

    draw = ImageDraw.Draw(image, "RGBA")
    petiole_points = [attach, leaf_base]
    draw.line(int_points([(x + px(4), y + px(5)) for x, y in petiole_points]), fill=(0, 0, 0, 42), width=round(petiole_width + px(4)))
    draw.line(int_points(petiole_points), fill=shift_color(fill, -18, -10, -16, 0), width=max(1, round(petiole_width)))
    draw.line(int_points(petiole_points), fill=shift_color(fill, 34, 36, 12, -60), width=max(1, round(petiole_width * 0.36)))

    polygon, left_edge, right_edge, leaf_direction, tip = build_leaf_points(
        leaf_base,
        angle_degrees,
        leaf_length,
        leaf_width,
        serrated=serrated,
        roundness=roundness,
        curve=curve,
        serration_amount=serration_amount,
    )

    margin = max(px(18), petiole_width * 4.0)
    left, top, right, bottom = polygon_bounds(polygon, margin)
    if right <= left or bottom <= top:
        return

    local_size = (right - left, bottom - top)
    local_points = [(x - left, y - top) for x, y in polygon]
    local_left = [(x - left, y - top) for x, y in left_edge]
    local_right = [(x - left, y - top) for x, y in right_edge]

    shadow = Image.new("RGBA", local_size, (0, 0, 0, 0))
    shadow_draw = ImageDraw.Draw(shadow, "RGBA")
    shadow_offset = px(5)
    shadow_draw.polygon([(x + shadow_offset, y + shadow_offset) for x, y in local_points], fill=(0, 0, 0, 54))
    shadow = shadow.filter(ImageFilter.GaussianBlur(px(3)))
    image.alpha_composite(shadow, (left, top))

    mask = Image.new("L", local_size, 0)
    mask_draw = ImageDraw.Draw(mask)
    mask_draw.polygon(int_points(local_points), fill=255)

    layer = Image.new("RGBA", local_size, (0, 0, 0, 0))
    layer_draw = ImageDraw.Draw(layer, "RGBA")
    layer_draw.polygon(int_points(local_points), fill=jitter_color(rng, fill, 10))

    highlight_points = local_left[:]
    highlight_points += [
        (
            (local_left[index][0] * 0.42) + (local_right[index][0] * 0.58),
            (local_left[index][1] * 0.42) + (local_right[index][1] * 0.58),
        )
        for index in range(len(local_right) - 1, -1, -1)
    ]
    layer_draw.polygon(int_points(highlight_points), fill=(210, 242, 150, 24))

    texture = Image.new("RGBA", local_size, (0, 0, 0, 0))
    texture_draw = ImageDraw.Draw(texture, "RGBA")
    for _ in range(70):
        t = rng.uniform(0.12, 0.92)
        center = leaf_midpoint((leaf_base[0] - left, leaf_base[1] - top), leaf_direction, leaf_length, curve, t)
        width = leaf_half_width(leaf_width, t, roundness) * rng.uniform(0.25, 0.9)
        side_scale = rng.choice((-1.0, 1.0))
        start = add(center, mul(side, side_scale * width * rng.uniform(0.1, 0.7)))
        end = add(start, mul(leaf_direction, rng.uniform(-px(10), px(26))))
        end = add(end, mul(side, side_scale * rng.uniform(px(6), px(20))))
        color = (24, 64, 30, rng.randint(18, 42)) if rng.random() < 0.7 else (224, 236, 156, rng.randint(10, 26))
        texture_draw.line(int_points([start, end]), fill=color, width=max(1, round(px(rng.uniform(0.8, 1.7)))))
    masked_alpha_composite(layer, texture, mask, (0, 0))

    vein_layer = Image.new("RGBA", local_size, (0, 0, 0, 0))
    vein_draw = ImageDraw.Draw(vein_layer, "RGBA")
    local_base = (leaf_base[0] - left, leaf_base[1] - top)
    local_tip = (tip[0] - left, tip[1] - top)
    midrib_color = shift_color(fill, 64, 68, 32, -8)
    vein_draw.line(int_points([local_base, local_tip]), fill=midrib_color, width=max(1, round(px(3.6))))
    vein_draw.line(int_points([local_base, local_tip]), fill=shift_color(fill, -34, -28, -18, -70), width=max(1, round(px(1.2))))

    vein_count = 8 if vein_style != "parallel" else 10
    for index in range(1, vein_count + 1):
        t = index / (vein_count + 1)
        center = leaf_midpoint(local_base, leaf_direction, leaf_length, curve, t)
        width = leaf_half_width(leaf_width, t, roundness) * 0.84
        forward = mul(leaf_direction, leaf_length * (0.025 if vein_style != "parallel" else 0.08))
        for side_sign in (-1.0, 1.0):
            if vein_style == "parallel":
                start = add(center, mul(side, side_sign * width * 0.08))
                end = add(add(center, mul(side, side_sign * width * 0.92)), forward)
            elif vein_style == "wrong":
                start = add(center, mul(leaf_direction, -leaf_length * 0.02))
                end = add(add(center, mul(side, side_sign * width)), mul(leaf_direction, -leaf_length * 0.07))
            else:
                start = center
                end = add(add(center, mul(side, side_sign * width)), forward)

            vein_draw.line(int_points([start, end]), fill=shift_color(fill, 38, 46, 20, -38), width=max(1, round(px(1.8))))

            if vein_style == "net" and 0.22 < t < 0.86:
                branch_end = add(end, mul(leaf_direction, leaf_length * 0.04))
                branch_end = add(branch_end, mul(side, -side_sign * width * 0.22))
                vein_draw.line(int_points([end, branch_end]), fill=(40, 88, 40, 70), width=max(1, round(px(0.9))))

    masked_alpha_composite(layer, vein_layer, mask, (0, 0))
    layer_draw.line(int_points(local_points + [local_points[0]]), fill=shift_color(fill, -42, -42, -30, -8), width=max(1, round(px(1.7))))

    image.alpha_composite(layer, (left, top))


def draw_berry(image: Image.Image, center: Point, radius: float, color: Color) -> None:
    draw = ImageDraw.Draw(image, "RGBA")
    x, y = center
    draw.ellipse(
        (round(x - radius + px(4)), round(y - radius + px(5)), round(x + radius + px(4)), round(y + radius + px(5))),
        fill=(0, 0, 0, 42),
    )
    draw.ellipse((round(x - radius), round(y - radius), round(x + radius), round(y + radius)), fill=color, outline=shift_color(color, -44, -32, -20))
    draw.ellipse(
        (round(x - radius * 0.42), round(y - radius * 0.52), round(x - radius * 0.08), round(y - radius * 0.18)),
        fill=shift_color(color, 58, 58, 70, -60),
    )


def draw_flower(image: Image.Image, rng: random.Random, center: Point, radius: float, petal_color: Color, center_color: Color) -> None:
    draw = ImageDraw.Draw(image, "RGBA")
    petal_count = 5 + rng.randint(0, 2)
    for index in range(petal_count):
        angle = (360.0 / petal_count) * index + rng.uniform(-8.0, 8.0)
        direction = unit(angle)
        petal_center = add(center, mul(direction, radius * 0.62))
        side = normal(direction)
        length = radius * rng.uniform(0.72, 1.0)
        width = radius * rng.uniform(0.34, 0.46)
        points = [
            add(petal_center, mul(direction, length)),
            add(petal_center, mul(side, width)),
            add(petal_center, mul(direction, -length * 0.45)),
            add(petal_center, mul(side, -width)),
        ]
        draw.polygon(int_points([(x + px(3), y + px(3)) for x, y in points]), fill=(0, 0, 0, 34))
        draw.polygon(int_points(points), fill=jitter_color(rng, petal_color, 9), outline=shift_color(petal_color, -32, -22, -18, -25))

    draw.ellipse(
        (
            round(center[0] - radius * 0.24),
            round(center[1] - radius * 0.24),
            round(center[0] + radius * 0.24),
            round(center[1] + radius * 0.24),
        ),
        fill=center_color,
        outline=shift_color(center_color, -35, -25, -10),
    )


def new_canvas() -> Image.Image:
    return Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))


def save_canvas(image: Image.Image, filename: str) -> None:
    output = image.resize(OUTPUT_SIZE, Image.Resampling.LANCZOS)
    output.save(OUTPUT_DIR / filename, optimize=True)


def draw_mint_like(
    image: Image.Image,
    rng: random.Random,
    *,
    filename: str,
    serrated: bool = True,
    paired: bool = True,
    offset_pairs: bool = False,
    curved: bool = False,
    rounder: bool = False,
    smooth: bool = False,
    wrong_veins: bool = False,
    hidden_bud: bool = False,
    extra_tip: bool = False,
    target_variant: int = 0,
) -> None:
    stem_color = (54, 116, 53, 255)
    leaf_color = (72, 136, 64, 255)

    if curved:
        p0 = p(0.57, 0.925)
        p1 = p(0.34, 0.74)
        p2 = p(0.69, 0.37)
        p3 = p(0.42, 0.135)
    else:
        curve_amount = rng.uniform(-0.012, 0.014)
        if target_variant == 1:
            curve_amount += 0.018
        elif target_variant == 2:
            curve_amount -= 0.014

        p0 = p(0.50, 0.925)
        p1 = p(0.50 + curve_amount, 0.68)
        p2 = p(0.47 - curve_amount * 0.5, 0.36)
        p3 = p(0.51, 0.135)
    stem = bezier_points(p0, p1, p2, p3)
    draw_polyline(image, stem, stem_color, px(18 if not curved else 16))

    pair_ts = [0.22, 0.38, 0.54, 0.70, 0.84]
    if target_variant == 1:
        pair_ts = [0.25, 0.41, 0.56, 0.72, 0.86]
    elif target_variant == 2:
        pair_ts = [0.20, 0.36, 0.53, 0.69, 0.83]

    for pair_index, t in enumerate(pair_ts):
        base = point_on_polyline(stem, t)
        tangent = polyline_tangent(stem, t)
        stem_angle = math.degrees(math.atan2(tangent[1], tangent[0]))
        length = px((345 if not rounder else 285) - pair_index * 18 + rng.uniform(-18, 18))
        width = px((112 if not rounder else 145) - pair_index * 4 + rng.uniform(-8, 8))
        if smooth:
            width *= 0.88
            length *= 0.92

        side_entries: list[tuple[float, float]]
        if paired:
            side_entries = [(-1.0, t), (1.0, t)]
        else:
            side_entries = [(-1.0 if pair_index % 2 == 0 else 1.0, t)]

        if offset_pairs:
            side_entries = [(-1.0, max(0.0, t - 0.035)), (1.0, min(1.0, t + 0.035))]

        for side_sign, side_t in side_entries:
            attach = point_on_polyline(stem, side_t)
            base_angle = -52.0 if side_sign > 0.0 else -128.0
            lower_spread = (0.5 - abs(0.5 - side_t)) * 28.0
            angle = base_angle + (lower_spread if side_sign > 0.0 else -lower_spread)
            angle += rng.uniform(-5.0, 5.0) + (stem_angle + 90.0) * 0.05
            draw_leaf(
                image,
                rng,
                attach,
                angle,
                length,
                width,
                leaf_color,
                serrated=serrated and not smooth,
                vein_style="wrong" if wrong_veins else "net",
                petiole_length=px(38 + rng.uniform(-5, 8)),
                petiole_width=px(7),
                roundness=0.63 if rounder else 0.78,
                curve=rng.uniform(-0.035, 0.04),
                serration_amount=0.105,
            )

    top_attach = point_on_polyline(stem, 0.93)
    draw_leaf(
        image,
        rng,
        top_attach,
        -88.0 + rng.uniform(-6.0, 6.0),
        px(300 if not extra_tip else 420),
        px(94 if not rounder else 116),
        shift_color(leaf_color, 8, 18, 6),
        serrated=serrated and not smooth,
        vein_style="wrong" if wrong_veins else "net",
        petiole_length=px(24),
        petiole_width=px(6),
        roundness=0.66 if rounder else 0.82,
        curve=rng.uniform(-0.02, 0.02),
    )

    if hidden_bud:
        bud_base = point_on_polyline(stem, 0.94)
        bud_stem = [bud_base, add(bud_base, (px(42), -px(95)))]
        draw_polyline(image, bud_stem, shift_color(stem_color, 4, 16, 2), px(7), shadow=False)
        for offset in (-px(20), 0, px(20)):
            draw_flower(
                image,
                rng,
                add(bud_stem[-1], (offset, rng.uniform(-px(10), px(12)))),
                px(28),
                (162, 94, 134, 238),
                (206, 176, 67, 255),
            )

    if extra_tip:
        tip_base = point_on_polyline(stem, 0.95)
        draw_leaf(
            image,
            rng,
            tip_base,
            -72.0,
            px(285),
            px(48),
            shift_color(leaf_color, 22, 30, 0),
            serrated=False,
            vein_style="parallel",
            petiole_length=px(20),
            petiole_width=px(5),
            roundness=0.92,
            curve=0.01,
        )

    save_canvas(image, filename)


def draw_flowering_stems(image: Image.Image, rng: random.Random, filename: str) -> None:
    base_color = (56, 112, 64, 255)
    leaf_color = (70, 128, 67, 255)
    stem_offsets = [-0.12, -0.06, 0.0, 0.065, 0.13]
    flower_centers: list[Point] = []

    for offset_index, offset in enumerate(stem_offsets):
        bottom = p(0.50 + offset, 0.91)
        top = p(0.48 + offset * 0.72 + rng.uniform(-0.015, 0.015), 0.20 + rng.uniform(-0.04, 0.04))
        stem = bezier_points(
            bottom,
            p(0.48 + offset * 0.9, 0.68),
            p(0.54 + offset * 0.65, 0.42),
            top,
            72,
        )
        draw_polyline(image, stem, jitter_color(rng, base_color, 8), px(11), shadow=offset_index == 0)
        flower_centers.append(top)

        for t in [0.24, 0.39, 0.54, 0.69]:
            attach = point_on_polyline(stem, t)
            side_sign = -1.0 if (offset_index + int(t * 100)) % 2 == 0 else 1.0
            angle = -118.0 if side_sign < 0.0 else -62.0
            draw_leaf(
                image,
                rng,
                attach,
                angle + rng.uniform(-12, 12),
                px(185 + rng.uniform(-20, 26)),
                px(42 + rng.uniform(-5, 7)),
                leaf_color,
                serrated=False,
                vein_style="parallel",
                petiole_length=px(18),
                petiole_width=px(4),
                roundness=0.94,
                curve=rng.uniform(-0.02, 0.02),
            )

    for center in flower_centers:
        for _ in range(3):
            draw_flower(
                image,
                rng,
                add(center, (rng.uniform(-px(34), px(34)), rng.uniform(-px(28), px(24)))),
                px(34 + rng.uniform(-4, 5)),
                (179, 103, 143, 242),
                (210, 178, 62, 255),
            )

    save_canvas(image, filename)


def draw_leaf_cluster(image: Image.Image, rng: random.Random, filename: str, *, smooth: bool = False) -> None:
    stem_color = (62, 108, 54, 255)
    leaf_color = (74, 132, 64, 255) if not smooth else (84, 139, 72, 255)
    main_stem = bezier_points(p(0.50, 0.88), p(0.49, 0.65), p(0.54, 0.39), p(0.50, 0.17), 86)
    draw_polyline(image, main_stem, stem_color, px(15))

    levels = [0.22, 0.34, 0.47, 0.60, 0.73, 0.84]
    for index, t in enumerate(levels):
        attach = point_on_polyline(main_stem, t)
        for side_sign in (-1.0, 1.0):
            if index == 0 and side_sign > 0:
                continue
            angle = -132.0 if side_sign < 0.0 else -48.0
            branch_end = add(attach, mul(unit(angle), px(120 + rng.uniform(-18, 22))))
            draw_polyline(image, [attach, branch_end], shift_color(stem_color, 10, 8, 2), px(7), shadow=False)
            draw_leaf(
                image,
                rng,
                branch_end,
                angle + rng.uniform(-8, 8),
                px((300 if not smooth else 270) + rng.uniform(-25, 35)),
                px((105 if not smooth else 92) + rng.uniform(-10, 12)),
                jitter_color(rng, leaf_color, 8),
                serrated=not smooth,
                vein_style="net",
                petiole_length=px(14),
                petiole_width=px(5),
                roundness=0.72 if not smooth else 0.86,
                curve=rng.uniform(-0.035, 0.035),
                serration_amount=0.07,
            )

    top = point_on_polyline(main_stem, 0.93)
    draw_leaf(
        image,
        rng,
        top,
        -92.0,
        px(275),
        px(88 if smooth else 98),
        shift_color(leaf_color, 9, 20, 3),
        serrated=not smooth,
        vein_style="net",
        petiole_length=px(18),
        petiole_width=px(5),
        roundness=0.76 if not smooth else 0.9,
    )

    save_canvas(image, filename)


def draw_slender_stems(image: Image.Image, rng: random.Random, filename: str) -> None:
    stem_color = (67, 96, 49, 255)
    leaf_color = (91, 132, 69, 255)
    stems: list[list[Point]] = []
    for offset in [-0.07, -0.025, 0.025, 0.07]:
        stem = bezier_points(
            p(0.50 + offset, 0.90),
            p(0.45 + offset, 0.67),
            p(0.56 + offset, 0.39),
            p(0.51 + offset * 0.45, 0.15),
            90,
        )
        stems.append(stem)
        draw_polyline(image, stem, jitter_color(rng, stem_color, 6), px(9), shadow=offset == -0.07)

    for stem_index, stem in enumerate(stems):
        for t in [0.18, 0.27, 0.36, 0.46, 0.57, 0.68, 0.79, 0.88]:
            attach = point_on_polyline(stem, t)
            for side_sign in (-1.0, 1.0):
                if rng.random() < 0.14:
                    continue
                angle = (-152.0 if side_sign < 0.0 else -28.0) + rng.uniform(-10, 10)
                draw_leaf(
                    image,
                    rng,
                    attach,
                    angle,
                    px(165 + rng.uniform(-25, 28)),
                    px(25 + rng.uniform(-4, 5)),
                    jitter_color(rng, leaf_color, 8),
                    serrated=False,
                    vein_style="parallel",
                    petiole_length=px(10),
                    petiole_width=px(3),
                    roundness=1.05,
                    curve=rng.uniform(-0.018, 0.018),
                )

    save_canvas(image, filename)


def draw_willow_stems(image: Image.Image, rng: random.Random, filename: str) -> None:
    stem_color = (75, 105, 56, 255)
    leaf_color = (112, 147, 89, 255)
    branch = bezier_points(p(0.38, 0.88), p(0.56, 0.67), p(0.43, 0.40), p(0.58, 0.15), 100)
    draw_polyline(image, branch, stem_color, px(12))

    for index, t in enumerate([0.16, 0.24, 0.32, 0.41, 0.50, 0.60, 0.70, 0.80, 0.88]):
        attach = point_on_polyline(branch, t)
        side_sign = -1.0 if index % 2 == 0 else 1.0
        angle = (-148.0 if side_sign < 0.0 else -35.0) + rng.uniform(-8, 8)
        draw_leaf(
            image,
            rng,
            attach,
            angle,
            px(310 + rng.uniform(-24, 28)),
            px(42 + rng.uniform(-5, 5)),
            jitter_color(rng, leaf_color, 6),
            serrated=False,
            vein_style="parallel",
            petiole_length=px(20),
            petiole_width=px(4),
            roundness=1.18,
            curve=rng.uniform(-0.028, 0.028),
        )

    save_canvas(image, filename)


def draw_dark_cluster(image: Image.Image, rng: random.Random, filename: str) -> None:
    stem_color = (42, 82, 52, 255)
    leaf_color = (37, 88, 59, 255)
    main = bezier_points(p(0.49, 0.89), p(0.53, 0.66), p(0.44, 0.38), p(0.52, 0.15), 96)
    draw_polyline(image, main, stem_color, px(14))

    for index, t in enumerate([0.18, 0.27, 0.36, 0.45, 0.55, 0.65, 0.75, 0.85]):
        attach = point_on_polyline(main, t)
        for side_sign in (-1.0, 1.0):
            branch_angle = (-142 if side_sign < 0.0 else -38) + rng.uniform(-8, 8)
            branch_end = add(attach, mul(unit(branch_angle), px(98 + rng.uniform(-14, 18))))
            draw_polyline(image, [attach, branch_end], stem_color, px(6), shadow=False)
            for needle_index in range(4):
                angle = branch_angle + rng.uniform(-34, 34)
                start = add(branch_end, mul(unit(branch_angle), px(needle_index * 6)))
                draw_leaf(
                    image,
                    rng,
                    start,
                    angle,
                    px(110 + rng.uniform(-15, 18)),
                    px(13 + rng.uniform(-2, 3)),
                    jitter_color(rng, leaf_color, 8),
                    serrated=False,
                    vein_style="parallel",
                    petiole_length=px(4),
                    petiole_width=px(2),
                    roundness=1.25,
                    curve=rng.uniform(-0.01, 0.01),
                )

        if index in (2, 4, 6):
            draw_berry(image, add(attach, (rng.uniform(-px(45), px(45)), rng.uniform(-px(25), px(25)))), px(24), (40, 55, 93, 246))

    save_canvas(image, filename)


def generate() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    mint_targets = [
        ("inspection_mint_target_a.png", 1201, 0),
        ("inspection_mint_target_b.png", 1202, 1),
        ("inspection_mint_target_c.png", 1203, 2),
    ]
    for filename, seed, variant in mint_targets:
        image = new_canvas()
        draw_mint_like(image, random.Random(seed), filename=filename, target_variant=variant)

    decoys = [
        ("inspection_mint_decoy_wrong_veins.png", 2101, {"wrong_veins": True}),
        ("inspection_mint_decoy_smooth_edge.png", 2102, {"smooth": True, "serrated": False}),
        ("inspection_mint_decoy_rounder_leaf.png", 2103, {"rounder": True}),
        ("inspection_mint_decoy_offset_leaf.png", 2104, {"paired": False}),
        ("inspection_mint_decoy_hidden_bud.png", 2105, {"hidden_bud": True}),
        ("inspection_mint_decoy_extra_tip.png", 2106, {"extra_tip": True}),
        ("inspection_mint_decoy_curved_stem.png", 2107, {"curved": True}),
        ("inspection_mint_decoy_alternate_pairs.png", 2108, {"offset_pairs": True}),
    ]
    for filename, seed, options in decoys:
        image = new_canvas()
        draw_mint_like(image, random.Random(seed), filename=filename, **options)

    forest_assets = [
        ("inspection_forest_flowering_stems.png", 3101, draw_flowering_stems),
        ("inspection_forest_leaf_cluster.png", 3102, lambda image, rng, filename: draw_leaf_cluster(image, rng, filename, smooth=False)),
        ("inspection_forest_slender_stems.png", 3103, draw_slender_stems),
        ("inspection_forest_willow_stems.png", 3104, draw_willow_stems),
        ("inspection_forest_dark_cluster.png", 3105, draw_dark_cluster),
        ("inspection_forest_smooth_cluster.png", 3106, lambda image, rng, filename: draw_leaf_cluster(image, rng, filename, smooth=True)),
    ]
    for filename, seed, renderer in forest_assets:
        image = new_canvas()
        renderer(image, random.Random(seed), filename)


if __name__ == "__main__":
    generate()
