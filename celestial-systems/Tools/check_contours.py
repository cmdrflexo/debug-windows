"""Numerical prototype of the spherical streamline evaluator."""
import argparse
import numpy as np
from PIL import Image

np.seterr(over="ignore", invalid="ignore")

def noise(n, radius, seed, size):
    p = n / np.linalg.norm(n, axis=-1, keepdims=True) * (radius / size)
    lower = np.floor(p).astype(np.int64)
    f = p-lower; f = f*f*f*(f*(f*6-15)+10)
    result = np.zeros(p.shape[:-1])
    for z in (0,1):
      for y in (0,1):
       for x in (0,1):
        c=lower+np.array([x,y,z]); h=np.full(result.shape,1469598103934665603,dtype=np.uint64)
        for j in range(3): h=(h^c[...,j].astype(np.uint64))*np.uint64(1099511628211)
        h=(h^np.uint64(seed&0xffffffff))*np.uint64(1099511628211)
        h^=h>>np.uint64(30); h*=np.uint64(0xbf58476d1ce4e5b9)
        h^=h>>np.uint64(27); h*=np.uint64(0x94d049bb133111eb); h^=h>>np.uint64(31)
        v=(h>>np.uint64(11)).astype(float)/9007199254740992.0
        w=(f[...,0] if x else 1-f[...,0])*(f[...,1] if y else 1-f[...,1])*(f[...,2] if z else 1-f[...,2])
        result += w*v
    return result

def normalize(v):
    return v/np.maximum(np.linalg.norm(v,axis=-1,keepdims=True),1e-30)

def flow(n,radius,seed,scale,complexity):
    v=np.stack([noise(n,radius,seed+1013,scale)*2-1,
      noise(n[..., [1,2,0]],radius,seed+3251,scale*.83)*2-1,
      noise(n[..., [2,0,1]],radius,seed+7919,scale*1.17)*2-1],axis=-1)
    swirl=np.cross(n,v); tangent=v-n*np.sum(n*v,axis=-1,keepdims=True)
    return normalize((1-complexity)*swirl+complexity*tangent)

def evaluate(n,seed=18548,radius=696340000,flow_scale=300000000,
             samples=24,fiber_width=30000000,complexity=.35,sharpness=2):
    n=normalize(n); half=int(np.clip(round(samples),4,48))//2
    step=np.clip(fiber_width/radius*.65,1e-7,.08)
    total=noise(n,radius,seed+15401,fiber_width); weight=np.ones(total.shape)
    forward=n.copy(); backward=n.copy()
    for i in range(1,half+1):
        forward=normalize(forward+flow(forward,radius,seed,flow_scale,complexity)*step)
        backward=normalize(backward-flow(backward,radius,seed,flow_scale,complexity)*step)
        w=.5+.5*np.cos(np.pi*i/(half+1))
        total += w*(noise(forward,radius,seed+15401,fiber_width)+noise(backward,radius,seed+15401,fiber_width)); weight += 2*w
    value=np.clip((np.clip(total/weight,0,1)-.5)*3+.5,0,1)
    return value**np.clip(sharpness,.25,8)

if __name__ == "__main__":
    p=argparse.ArgumentParser(); p.add_argument("--render"); args=p.parse_args()
    rng=np.random.default_rng(431); n=rng.normal(size=(10000,3)); a=evaluate(n)
    assert np.isfinite(a).all() and a.min()>=0 and a.max()<=1
    assert np.array_equal(a,evaluate(n)); assert np.max(abs(a-evaluate(n*7)))<1e-10
    assert np.max(abs(a-evaluate(n,radius=1392680000,flow_scale=600000000,fiber_width=60000000)))<1e-10
    assert np.mean(abs(a-evaluate(n,seed=27)))>.05
    max_gap=0
    for i in range(3):
      for j in range(i+1,3):
       for si in (-1,1):
        for sj in (-1,1):
         edge=np.zeros((257,3)); edge[:,i]=si; edge[:,j]=sj; edge[:,3-i-j]=np.linspace(-1,1,257)
         left=edge.copy(); right=edge.copy(); left[:,j]*=1-1e-10; right[:,i]*=1-1e-10
         max_gap=max(max_gap,float(np.max(abs(evaluate(left)-evaluate(right)))))
    assert max_gap<1e-6; print(f"PASS: range/determinism/scale/seed/edges; max gap={max_gap:.3g}")
    if args.render:
        width=512; y,x=np.mgrid[-1:1:complex(width),-1:1:complex(width)]; disk=x*x+y*y<=1
        rays=np.stack([x,-y,np.sqrt(np.maximum(0,1-x*x-y*y))],axis=-1); val=evaluate(rays)
        rgb=np.array([100,40,12])+(np.array([250,220,115])-np.array([100,40,12]))*val[...,None]; rgb[~disk]=0
        Image.fromarray(rgb.astype(np.uint8)).save(args.render)
