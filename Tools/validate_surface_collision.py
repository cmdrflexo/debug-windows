#!/usr/bin/env python3
"""Independent numeric checks for Milestone 5; does not compile C# or run PhysX.

Run with Python 3. The Unity collision component also runs the production C#
geometry diagnostic on initialization and exposes its result in the debug recipe.
"""
import math
import struct
import unittest


def add(a, b):
    return tuple(x + y for x, y in zip(a, b))


def sub(a, b):
    return tuple(x - y for x, y in zip(a, b))


def mul(a, s):
    return tuple(x * s for x in a)


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def cross(a, b):
    return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])


def length(a):
    return math.sqrt(dot(a, a))


def unit(a):
    return mul(a, 1 / length(a))


def direction(face, u, v):
    u, v = math.tan(u * math.pi / 4), math.tan(v * math.pi / 4)
    return unit(((1, v, -u), (-1, v, u), (u, 1, -v),
                 (u, -1, v), (u, v, 1), (-u, v, -1))[face])


def address(point, face=None):
    x, y, z = point
    components = (x, -x, y, -y, z, -z)
    if face is None:
        face = max(range(6), key=lambda i: components[i])
    u, v = ((-z, y), (z, y), (x, -z), (x, z), (x, y), (-x, y))[face]
    return face, math.atan(u / components[face])*4/math.pi, math.atan(v / components[face])*4/math.pi


def reference(patch):
    face, level, x, y = patch
    n = 2**level
    return direction(face, -1+(2*x+1)/n, -1+(2*y+1)/n)


def vertex(patch, x, y, radius):
    face, level, px, py = patch
    n = 2**level
    elevation = math.sin(x*1.7+y*.9)*min(120, radius*.001)
    return mul(direction(face, -1+2*(px+x/32)/n, -1+2*(py+y/32)/n), radius+elevation)


def ray_triangle(ray, a, b, c):
    ab, ac = sub(b, a), sub(c, a)
    p = cross(ray, ac)
    det = dot(ab, p)
    if abs(det) < 1e-18:
        return None
    origin = mul(a, -1)
    u = dot(origin, p)/det
    q = cross(origin, ab)
    v, distance = dot(ray, q)/det, dot(ac, q)/det
    if u < -1e-7 or v < -1e-7 or u+v > 1.0000001 or distance <= 0:
        return None
    return mul(ray, distance)


def query(patch, point, radius):
    face, level, px, py = patch
    _, u, v = address(point, face)
    x = min(31, max(0, math.floor(((u+1)/2*2**level-px)*32)))
    y = min(31, max(0, math.floor(((v+1)/2*2**level-py)*32)))
    a, b, c, d = [vertex(patch, xx, yy, radius) for xx, yy in
                   ((x, y), (x+1, y), (x, y+1), (x+1, y+1))]
    return ray_triangle(unit(point), a, b, c) or ray_triangle(unit(point), b, d, c)


def collect(center, radius, coverage, level, limit):
    result = set()

    def visit(patch):
        face, depth, x, y = patch
        bound = math.pi*.5/2**depth
        if length(sub(center, reference(patch)))*radius > coverage+bound*radius:
            return True
        if depth == level:
            if len(result) >= limit:
                return False
            result.add(patch)
            return True
        return all(visit((face, depth+1, x*2+dx, y*2+dy))
                   for dy in (0, 1) for dx in (0, 1))

    complete = all(visit((f, 0, 0, 0)) for f in range(6))
    return complete, result


class CollisionMathTests(unittest.TestCase):
    def test_nearby_positions_across_large_cell_boundary(self):
        cell = 9007199254741000
        size = 1e9
        before = size*.5-.125
        after = before+.25-size
        precise_delta = float((cell+1)-cell)*size+after-before
        rounded_delta = (float(cell+1)-float(cell))*size+after-before
        self.assertEqual(precise_delta, .25)
        self.assertNotEqual(rounded_delta, .25)

    def test_queries_intersect_known_triangle_points(self):
        maximum = 0
        for radius in (1000.0, 6371000.0):
            for face in range(6):
                for level in (0, 8, 16, 20):
                    patch = (face, level, 2**level//2, 2**level//2)
                    for y in range(0, 32, 5):
                        for x in range(0, 32, 5):
                            a, b, c, d = [vertex(patch, xx, yy, radius) for xx, yy in
                                           ((x, y), (x+1, y), (x, y+1), (x+1, y+1))]
                            for triangle in ((a, b, c), (b, d, c)):
                                expected = add(add(mul(triangle[0], .2), mul(triangle[1], .3)), mul(triangle[2], .5))
                                actual = query(patch, mul(expected, 1.01), radius)
                                self.assertIsNotNone(actual, (patch, x, y))
                                error = length(sub(actual, expected))
                                maximum = max(maximum, error)
                                self.assertLess(error, max(.0001, radius*1e-10))
        print(f"4,704 radial triangle checks; maximum error {maximum:.3e} m")

    def test_corner_coverage_and_bounded_overflow(self):
        radius = 6371000.0
        level = math.ceil(math.log2(math.pi*.5*radius/(32*8)))
        for x in (-1, 1):
            for y in (-1, 1):
                for z in (-1, 1):
                    center = unit((x, y, z))
                    complete, patches = collect(center, radius, 256, level, 128)
                    self.assertTrue(complete)
                    tangent = unit(cross(center, (0, 1, 0)))
                    bitangent = cross(center, tangent)
                    for i in range(32):
                        theta = i*math.pi/16
                        offset = add(mul(tangent, math.cos(theta)), mul(bitangent, math.sin(theta)))
                        point = add(mul(center, math.cos(250/radius)), mul(offset, math.sin(250/radius)))
                        f, u, v = address(point)
                        px, py = [min(2**level-1, max(0, math.floor((q+1)/2*2**level))) for q in (u, v)]
                        self.assertIn((f, level, px, py), patches)
        complete, patches = collect(unit((1, 1, 1)), radius, 1e6, level, 4)
        self.assertFalse(complete)
        self.assertEqual(len(patches), 4)

    def test_local_mesh_precision_and_origin_rebase(self):
        def f32(v):
            return struct.unpack('f', struct.pack('f', v))[0]
        radius = 6371000.0
        patch = (4, 16, 32768, 32768)
        pivot = mul(reference(patch), radius)
        offset = (-pivot[0]+10, -pivot[1]+20, -pivot[2]+30)
        for y in range(33):
            for x in range(33):
                p = vertex(patch, x, y, radius)
                local = tuple(map(f32, sub(p, pivot)))
                body_position = tuple(map(f32, add(pivot, offset)))
                reconstructed = add(body_position, local)
                self.assertLess(length(sub(reconstructed, add(p, offset))), .0001)
                shift = (1000, -2000, 3000)
                actor = add(reconstructed, (1, 2, 3))
                self.assertLess(length(sub(sub(add(actor, shift), add(reconstructed, shift)), (1, 2, 3))), 1e-9)


if __name__ == '__main__':
    unittest.main(verbosity=2)
