"""Numerical prototype of C# contour evaluator; not a Unity compilation test."""
import numpy as np
from PIL import Image
import argparse

np.seterr(over='ignore')

def noise(n, radius, seed, size):
    p = n / np.linalg.norm(n, axis=-1, keepdims=True) * (radius / size)
    lower = np.floor(p).astype(np.int64)
    f = p - lower
    f = f*f*f*(f*(f*6-15)+10)
    result = np.zeros(p.shape[:-1])
    for z in (0, 1):
        for y in (0, 1):
            for x in (0, 1):
                c = lower + np.array([x,y,z])
                h = np.full(result.shape, 1469598103934665603, dtype=np.uint64)
                for j in range(3):
                    h = (h ^ c[...,j].astype(np.uint64)) * np.uint64(1099511628211)
                h = (h ^ np.uint64(seed & 0xffffffff)) * np.uint64(1099511628211)
                h ^= h >> np.uint64(30)
                h *= np.uint64(0xbf58476d1ce4e5b9)
                h ^= h >> np.uint64(27)
                h *= np.uint64(0x94d049bb133111eb)
                h ^= h >> np.uint64(31)
                v = (h >> np.uint64(11)).astype(float) / 9007199254740992.0
                w = (f[...,0] if x else 1-f[...,0])
                w = w * (f[...,1] if y else 1-f[...,1])
                w = w * (f[...,2] if z else 1-f[...,2])
                result += w * v
    return result

rotation = np.array([[1,2,2],[2,1,-2],[-2,2,-1]])/3

def evaluate(n, seed=18548, radius=696340000, region=300000000,
             bands=12, size=30000000, distortion=.15, sharpness=2):
    n = n / np.linalg.norm(n, axis=-1, keepdims=True)
    a = n @ rotation.T
    b = n[..., [2,0,1]] @ rotation.T
    field = .65*noise(a,radius,seed,region)+.35*noise(b,radius,seed+1013,region*.73)
    detail = noise(b,radius,seed+7919,size)*2-1
    phase = field*bands+detail*distortion
    return np.clip(.5+.5*np.cos(2*np.pi*phase),0,1)**sharpness

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--render', help='Optional output PNG path')
    args = parser.parse_args()
    rng = np.random.default_rng(431)
    n = rng.normal(size=(10000,3))
    a = evaluate(n)
    assert np.isfinite(a).all() and a.min()>=0 and a.max()<=1
    assert np.array_equal(a,evaluate(n))
    assert np.max(abs(a-evaluate(n*7))) < 1e-10
    assert np.max(abs(a-evaluate(n,radius=696340000*2,
        region=600000000,size=60000000))) < 1e-10
    assert np.mean(abs(a-evaluate(n,seed=27))) > .1
    # Shared cube-edge rays, approached from each neighboring face.
    # Independent of the production mapping; verifies direction-space continuity only.
    max_gap = 0
    for i in range(3):
        for j in range(i+1,3):
            k = 3-i-j
            for si in (-1,1):
                for sj in (-1,1):
                    edge = np.zeros((257,3))
                    edge[:,i],edge[:,j] = si,sj
                    edge[:,k] = np.linspace(-1,1,257)
                    left,right = edge.copy(),edge.copy()
                    left[:,j] *= 1-1e-10
                    right[:,i] *= 1-1e-10
                    max_gap = max(max_gap,float(np.max(abs(evaluate(left)-evaluate(right)))))
    assert max_gap < 1e-6
    print(f'PASS: 10,000 range/determinism/scale/seed samples; 12 edge approaches; max gap={max_gap:.3g}')
    if not args.render:
        raise SystemExit(0)
    # Render an orthographic sphere and local direction-space patch from the same evaluator.
    width=600
    y,x=np.mgrid[-1:1:complex(width),-1:1:complex(width)]
    disk=x*x+y*y<=1
    z=np.sqrt(np.maximum(0,1-x*x-y*y))
    sphere=np.stack([x,-y,z],axis=-1)
    patch=np.stack([x*.25+.35,-y*.25+.1,np.ones_like(x)],axis=-1)
    panels=[]
    for rays,mask in [(sphere,disk),(patch,np.ones_like(disk))]:
        val=evaluate(rays)
        dark=np.array([85,38,12])
        bright=np.array([250,213,108])
        rgb=dark+(bright-dark)*val[...,None]
        rgb[~mask]=0
        panels.append(rgb.astype(np.uint8))
    Image.fromarray(np.concatenate(panels,axis=1)).save(args.render)
